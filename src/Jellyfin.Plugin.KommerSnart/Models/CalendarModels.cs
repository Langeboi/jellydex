namespace Jellyfin.Plugin.KommerSnart.Models;

public sealed class CalendarResponse
{
    public string Region { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<CalendarItem> Items { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class CalendarItem
{
    public int TmdbId { get; init; }

    public string MediaType { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Overview { get; init; }

    public string? PosterPath { get; init; }

    public string? ReleaseDate { get; init; }

    public string DateSource { get; init; } = string.Empty;

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    public string? EpisodeTitle { get; init; }

    public int RequestStatus { get; init; }

    public int MediaStatus { get; init; }
}

public sealed class PatchRequestPayload
{
    public string? Contents { get; set; }
}
