using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.KommerSnart.Configuration;
using Jellyfin.Plugin.KommerSnart.Helpers;
using Jellyfin.Plugin.KommerSnart.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KommerSnart.Services;

public sealed class CalendarService
{
    private const int PageSize = 100;
    private const int MaximumRequests = 1000;
    private const int DigitalReleaseType = 4;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<CalendarService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CalendarResponse> GetCalendarAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var config = GetValidatedConfiguration();
        var cacheKey = BuildCacheKey(config);
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out CalendarResponse? cached) && cached is not null)
        {
            return cached;
        }

        var requests = await GetRequestsAsync(config, cancellationToken).ConfigureAwait(false);
        var items = new ConcurrentBag<CalendarItem>();
        var warnings = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            requests,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 6
            },
            async (request, token) =>
            {
                try
                {
                    var item = await BuildCalendarItemAsync(config, request, token).ConfigureAwait(false);
                    if (item is not null && IsInsideWindow(config, item.ReleaseDate))
                    {
                        items.Add(item);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        exception,
                        "Kommer Snart could not load Seerr details for {MediaType} {TmdbId}.",
                        request.MediaType,
                        request.TmdbId);
                    warnings.Add($"Kunne ikke hente {request.MediaType} {request.TmdbId}.");
                }
            }).ConfigureAwait(false);

        var response = new CalendarResponse
        {
            Region = NormalizeRegion(config.Region),
            GeneratedAt = DateTimeOffset.UtcNow,
            Items = items
                .OrderBy(item => item.ReleaseDate is null)
                .ThenBy(item => item.ReleaseDate, StringComparer.Ordinal)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            Warnings = warnings.Distinct(StringComparer.Ordinal).Take(10).ToArray()
        };

        _cache.Set(
            cacheKey,
            response,
            TimeSpan.FromMinutes(Math.Clamp(config.CacheMinutes, 1, 180)));
        return response;
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        var config = GetValidatedConfiguration();
        using var document = await GetJsonAsync(config, "auth/me", cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Seerr returned an unexpected response.");
        }
    }

    private static PluginConfiguration GetValidatedConfiguration()
    {
        var config = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Kommer Snart is disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.SeerrApiKey))
        {
            throw new InvalidOperationException("The Seerr API key has not been configured.");
        }

        if (!Uri.TryCreate(config.SeerrUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("The Seerr URL must be a valid HTTP or HTTPS URL.");
        }

        return config;
    }

    private async Task<IReadOnlyList<RequestReference>> GetRequestsAsync(
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var requests = new List<RequestReference>();
        for (var skip = 0; skip < MaximumRequests; skip += PageSize)
        {
            var path = string.Create(
                CultureInfo.InvariantCulture,
                $"request?take={PageSize}&skip={skip}&filter=all&sort=added&sortDirection=desc");
            using var document = await GetJsonAsync(config, path, cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Seerr's request response did not contain a results array.");
            }

            var count = 0;
            foreach (var result in results.EnumerateArray())
            {
                count++;
                var status = JsonHelpers.Int32(result, "status");
                if (status == 3 || (!config.IncludePendingRequests && status != 2))
                {
                    continue;
                }

                if (!result.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var mediaType = JsonHelpers.String(result, "type")
                    ?? JsonHelpers.String(media, "mediaType")
                    ?? string.Empty;
                var tmdbId = JsonHelpers.Int32(media, "tmdbId");
                if (tmdbId <= 0 || (mediaType != "movie" && mediaType != "tv"))
                {
                    continue;
                }

                requests.Add(new RequestReference(
                    tmdbId,
                    mediaType,
                    status,
                    JsonHelpers.Int32(media, "status")));
            }

            if (count < PageSize)
            {
                break;
            }
        }

        return requests
            .GroupBy(request => (request.MediaType, request.TmdbId))
            .Select(group => group.OrderByDescending(request => request.RequestStatus).First())
            .ToArray();
    }

    private async Task<CalendarItem?> BuildCalendarItemAsync(
        PluginConfiguration config,
        RequestReference request,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            config,
            $"{request.MediaType}/{request.TmdbId}",
            cancellationToken).ConfigureAwait(false);
        var details = document.RootElement;

        return request.MediaType == "movie"
            ? BuildMovie(config, request, details)
            : BuildSeries(request, details);
    }

    private static CalendarItem BuildMovie(
        PluginConfiguration config,
        RequestReference request,
        JsonElement details)
    {
        DateOnly? releaseDate = null;
        if (details.TryGetProperty("releases", out var releases)
            && releases.TryGetProperty("results", out var regions)
            && regions.ValueKind == JsonValueKind.Array)
        {
            var region = regions.EnumerateArray().FirstOrDefault(candidate => string.Equals(
                JsonHelpers.String(candidate, "iso_3166_1"),
                NormalizeRegion(config.Region),
                StringComparison.OrdinalIgnoreCase));
            if (region.ValueKind == JsonValueKind.Object
                && region.TryGetProperty("release_dates", out var dates)
                && dates.ValueKind == JsonValueKind.Array)
            {
                releaseDate = dates.EnumerateArray()
                    .Where(date => JsonHelpers.Int32(date, "type") == DigitalReleaseType)
                    .Select(date => JsonHelpers.Date(date, "release_date"))
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .OrderBy(date => date)
                    .Cast<DateOnly?>()
                    .FirstOrDefault();
            }
        }

        return new CalendarItem
        {
            TmdbId = request.TmdbId,
            MediaType = request.MediaType,
            Title = JsonHelpers.String(details, "title") ?? $"Film {request.TmdbId}",
            Overview = JsonHelpers.String(details, "overview"),
            PosterPath = JsonHelpers.String(details, "posterPath"),
            ReleaseDate = releaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateSource = releaseDate.HasValue ? "digital" : "unknown",
            RequestStatus = request.RequestStatus,
            MediaStatus = request.MediaStatus
        };
    }

    private static CalendarItem BuildSeries(RequestReference request, JsonElement details)
    {
        DateOnly? airDate = null;
        int? seasonNumber = null;
        int? episodeNumber = null;
        string? episodeTitle = null;
        if (details.TryGetProperty("nextEpisodeToAir", out var episode)
            && episode.ValueKind == JsonValueKind.Object)
        {
            airDate = JsonHelpers.Date(episode, "airDate");
            seasonNumber = JsonHelpers.Int32(episode, "seasonNumber");
            episodeNumber = JsonHelpers.Int32(episode, "episodeNumber");
            episodeTitle = JsonHelpers.String(episode, "name");
        }

        return new CalendarItem
        {
            TmdbId = request.TmdbId,
            MediaType = request.MediaType,
            Title = JsonHelpers.String(details, "name") ?? $"Serie {request.TmdbId}",
            Overview = JsonHelpers.String(details, "overview"),
            PosterPath = JsonHelpers.String(details, "posterPath"),
            ReleaseDate = airDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateSource = airDate.HasValue ? "nextEpisode" : "unknown",
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            EpisodeTitle = episodeTitle,
            RequestStatus = request.RequestStatus,
            MediaStatus = request.MediaStatus
        };
    }

    private async Task<JsonDocument> GetJsonAsync(
        PluginConfiguration config,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var url = $"{config.SeerrUrl.TrimEnd('/')}/api/v1/{relativePath.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Api-Key", config.SeerrApiKey.Trim());
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Seerr returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static bool IsInsideWindow(PluginConfiguration config, string? releaseDate)
    {
        if (releaseDate is null
            || !DateOnly.TryParseExact(
                releaseDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return date >= today.AddDays(-Math.Clamp(config.DaysBack, 0, 90))
            && date <= today.AddDays(Math.Clamp(config.DaysAhead, 7, 730));
    }

    private static string NormalizeRegion(string? region)
    {
        var normalized = region?.Trim().ToUpperInvariant();
        return normalized is { Length: 2 } ? normalized : "NO";
    }

    private static string BuildCacheKey(PluginConfiguration config)
    {
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.SeerrApiKey)))[..8];
        return string.Create(
            CultureInfo.InvariantCulture,
            $"kommer-snart:{config.SeerrUrl}:{NormalizeRegion(config.Region)}:{config.IncludePendingRequests}:{config.DaysAhead}:{config.DaysBack}:{keyHash}");
    }

    private sealed record RequestReference(
        int TmdbId,
        string MediaType,
        int RequestStatus,
        int MediaStatus);
}
