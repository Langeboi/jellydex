using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.KommerSnart.Models;

namespace Jellyfin.Plugin.KommerSnart.Helpers;

public static partial class TransformationPatches
{
    public static string IndexHtml(PatchRequestPayload payload)
    {
        var contents = payload.Contents ?? string.Empty;
        if (contents.Contains("kommer-snart-plugin-assets", StringComparison.Ordinal))
        {
            return contents;
        }

        var css = ReadResource("Jellyfin.Plugin.KommerSnart.Web.kommer-snart.css");
        var script = ReadResource("Jellyfin.Plugin.KommerSnart.Web.kommer-snart.js");
        var assets = $"<style id=\"kommer-snart-plugin-assets\">{css}</style><script defer>{script}</script>";

        return ClosingHeadRegex().Replace(contents, assets + "$1", 1);
    }

    public static string HomeHtmlChunk(PatchRequestPayload payload)
    {
        var contents = payload.Contents ?? string.Empty;
        if (contents.Contains("id=\"kommerSnartTab\"", StringComparison.Ordinal))
        {
            return contents;
        }

        var tab = ReadResource("Jellyfin.Plugin.KommerSnart.Web.tab.html")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace("'undefined'", "\\'undefined'", StringComparison.Ordinal);

        return FavoritesTabRegex().Replace(contents, "$1" + tab, 1);
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource {name} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex("(</head>)", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingHeadRegex();

    [GeneratedRegex("(<div class=\\\"tabContent pageTabContent\\\" id=\\\"favoritesTab\\\" data-index=\\\"1\\\">\\s*<div class=\\\"sections\\\"></div>\\s*</div>)")]
    private static partial Regex FavoritesTabRegex();
}
