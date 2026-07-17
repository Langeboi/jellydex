using System.Reflection;
using System.Runtime.Loader;
using Jellyfin.Plugin.KommerSnart.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.KommerSnart.Services;

public sealed class FileTransformationHostedService : IHostedService
{
    private readonly ILogger<FileTransformationHostedService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _registrationTask;

    public FileTransformationHostedService(ILogger<FileTransformationHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _registrationTask = Task.Run(() => RegisterWithRetryAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        if (_registrationTask is not null)
        {
            await Task.WhenAny(_registrationTask, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken))
                .ConfigureAwait(false);
        }
    }

    private async Task RegisterWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 12 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            if (TryRegister())
            {
                _logger.LogInformation("Kommer Snart registered its Jellyfin Web transformations.");
                return;
            }

            if (attempt < 12)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogWarning(
            "Kommer Snart could not find File Transformation. Install it from "
            + "https://www.iamparadox.dev/jellyfin/plugins/manifest.json and restart Jellyfin.");
    }

    private static bool TryRegister()
    {
        Assembly? assembly = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .FirstOrDefault(candidate => candidate.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) == true);
        var interfaceType = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
        var method = interfaceType?.GetMethod("RegisterTransformation");
        if (method is null)
        {
            return false;
        }

        var assemblyName = typeof(FileTransformationHostedService).Assembly.FullName;
        var callbackClass = typeof(TransformationPatches).FullName;
        method.Invoke(null, [new JObject
        {
            { "id", "25a94210-487c-441d-a59f-fb1f4f3751eb" },
            { "fileNamePattern", "index.html" },
            { "callbackAssembly", assemblyName },
            { "callbackClass", callbackClass },
            { "callbackMethod", nameof(TransformationPatches.IndexHtml) }
        }]);
        method.Invoke(null, [new JObject
        {
            { "id", "86175fb3-5f93-4d55-a10c-9f596547444f" },
            { "fileNamePattern", "home-html\\..*\\.chunk\\.js" },
            { "callbackAssembly", assemblyName },
            { "callbackClass", callbackClass },
            { "callbackMethod", nameof(TransformationPatches.HomeHtmlChunk) }
        }]);
        return true;
    }
}
