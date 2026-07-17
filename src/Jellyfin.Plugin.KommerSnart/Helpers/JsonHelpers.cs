using System.Globalization;
using System.Text.Json;

namespace Jellyfin.Plugin.KommerSnart.Helpers;

internal static class JsonHelpers
{
    public static string? String(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static int Int32(JsonElement element, string name, int fallback = 0)
    {
        return element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : fallback;
    }

    public static DateOnly? Date(JsonElement element, string name)
    {
        var value = String(element, name);
        if (string.IsNullOrWhiteSpace(value) || value.Length < 10)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            value[..10],
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }
}
