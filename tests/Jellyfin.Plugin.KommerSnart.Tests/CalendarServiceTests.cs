using System.Text.Json;
using Jellyfin.Plugin.KommerSnart.Services;
using Xunit;

namespace Jellyfin.Plugin.KommerSnart.Tests;

public sealed class CalendarServiceTests
{
    [Fact]
    public void ParseRequestPageUsesQueriedMediaTypeWhenResponseOmitsIt()
    {
        using var document = JsonDocument.Parse("""
            [
              { "status": 2, "media": { "tmdbId": 101, "status": 4 } },
              { "status": 1, "media": { "tmdbId": 202, "status": 2 } }
            ]
            """);

        var requests = CalendarService.ParseRequestPage(document.RootElement, "movie", true);

        Assert.Collection(
            requests,
            request =>
            {
                Assert.Equal(101, request.TmdbId);
                Assert.Equal("movie", request.MediaType);
            },
            request =>
            {
                Assert.Equal(202, request.TmdbId);
                Assert.Equal("movie", request.MediaType);
            });
    }

    [Fact]
    public void ParseRequestPageAppliesApprovalFilterAndSkipsDeclinedRequests()
    {
        using var document = JsonDocument.Parse("""
            [
              { "status": 1, "media": { "tmdbId": 101, "status": 2 } },
              { "status": 2, "media": { "tmdbId": 202, "status": 3 } },
              { "status": 3, "media": { "tmdbId": 303, "status": 1 } }
            ]
            """);

        var requests = CalendarService.ParseRequestPage(document.RootElement, "tv", false);

        var request = Assert.Single(requests);
        Assert.Equal(202, request.TmdbId);
        Assert.Equal("tv", request.MediaType);
        Assert.Equal(2, request.RequestStatus);
        Assert.Equal(3, request.MediaStatus);
    }
}
