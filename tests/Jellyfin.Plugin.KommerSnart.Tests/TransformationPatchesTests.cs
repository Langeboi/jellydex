using Jellyfin.Plugin.KommerSnart.Helpers;
using Jellyfin.Plugin.KommerSnart.Models;
using Xunit;

namespace Jellyfin.Plugin.KommerSnart.Tests;

public sealed class TransformationPatchesTests
{
    [Fact]
    public void IndexHtmlInjectsAssetsOnlyOnce()
    {
        var original = "<html><head><title>Jellyfin</title></head><body></body></html>";

        var first = TransformationPatches.IndexHtml(new PatchRequestPayload { Contents = original });
        var second = TransformationPatches.IndexHtml(new PatchRequestPayload { Contents = first });

        Assert.Contains("id=\"kommer-snart-plugin-assets\"", first, StringComparison.Ordinal);
        Assert.Contains("window.kommerSnartPlugin", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
    }

    [Fact]
    public void HomeChunkInjectsTabOnlyOnce()
    {
        var original = """
            before<div class="tabContent pageTabContent" id="favoritesTab" data-index="1">
              <div class="sections"></div>
            </div>after
            """;

        var first = TransformationPatches.HomeHtmlChunk(new PatchRequestPayload { Contents = original });
        var second = TransformationPatches.HomeHtmlChunk(new PatchRequestPayload { Contents = first });

        Assert.Contains("id=\"kommerSnartTab\"", first, StringComparison.Ordinal);
        Assert.Contains("id=\"kommerSnartRoot\"", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
    }
}
