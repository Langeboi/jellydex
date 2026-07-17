using System.Text.Json;
using Jellyfin.Plugin.KommerSnart.Helpers;
using Xunit;

namespace Jellyfin.Plugin.KommerSnart.Tests;

public sealed class JsonHelpersTests
{
    [Fact]
    public void ReadsExpectedPrimitiveValues()
    {
        using var document = JsonDocument.Parse("""
            {
              "title": "Arrival",
              "status": 2,
              "releaseDate": "2026-07-17T12:30:00.000Z"
            }
            """);

        Assert.Equal("Arrival", JsonHelpers.String(document.RootElement, "title"));
        Assert.Equal(2, JsonHelpers.Int32(document.RootElement, "status"));
        Assert.Equal(new DateOnly(2026, 7, 17), JsonHelpers.Date(document.RootElement, "releaseDate"));
    }

    [Fact]
    public void ReturnsFallbacksForMissingOrMalformedValues()
    {
        using var document = JsonDocument.Parse("""
            {
              "title": 42,
              "status": "approved",
              "releaseDate": "soon"
            }
            """);

        Assert.Null(JsonHelpers.String(document.RootElement, "title"));
        Assert.Equal(7, JsonHelpers.Int32(document.RootElement, "status", 7));
        Assert.Null(JsonHelpers.Date(document.RootElement, "releaseDate"));
        Assert.Null(JsonHelpers.String(document.RootElement, "missing"));
    }
}
