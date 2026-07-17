using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.KommerSnart.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = true;

    public string SeerrUrl { get; set; } = "http://10.10.100.3:5050";

    public string SeerrApiKey { get; set; } = string.Empty;

    public string Region { get; set; } = "NO";

    public bool IncludePendingRequests { get; set; } = true;

    public int DaysAhead { get; set; } = 365;

    public int DaysBack { get; set; } = 14;

    public int CacheMinutes { get; set; } = 15;
}
