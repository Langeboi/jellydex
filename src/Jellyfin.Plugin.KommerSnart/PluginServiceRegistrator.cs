using Jellyfin.Plugin.KommerSnart.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.KommerSnart;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient();
        serviceCollection.AddMemoryCache();
        serviceCollection.AddSingleton<CalendarService>();
        serviceCollection.AddHostedService<FileTransformationHostedService>();
    }
}
