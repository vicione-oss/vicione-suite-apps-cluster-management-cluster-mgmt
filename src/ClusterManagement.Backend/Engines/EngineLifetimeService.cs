using System.Collections.Immutable;
using System.IO.Abstractions;
using ClusterManagement.Public;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal sealed class EngineLifetimeService(IServiceProvider services, ILogger<EngineLifetimeService> logger) : IHostedLifecycleService
{
    private readonly IServiceProvider _services = services;

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();

        try
        {
            var downloader = scope.ServiceProvider.GetServices<IHostedService>().OfType<IDownloader>();
            await Task.WhenAll(downloader.Select(d => d.Task));
        }
        catch (Exception ex)
        {
            logger.DownloadsFailed(ex);
            return;
        }

        try
        {
            var engineLifetime = scope.ServiceProvider.GetRequiredService<IEngineLifetime>();
            await engineLifetime.Start(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.StartingEnginesFailed(ex);
        }
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        if (ShouldStopEngineHosts(fileSystem))
        {
            var engineLifetime = scope.ServiceProvider.GetRequiredService<IEngineLifetime>();
            await engineLifetime.Stop(cancellationToken);
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool ShouldStopEngineHosts(IFileSystem fileSystem)
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY") ?? "/run/vicione-suite";

        ImmutableArray<string> fileNames = ["os-pgk-upgrade", "suite-ui-restart",];
        foreach (var fileName in fileNames)
        {
            var path = fileSystem.Path.Combine(runtimeDirectory, fileName);
            if (fileSystem.File.Exists(path))
            {
                logger.KeepEnginesRunning(fileName);
                fileSystem.File.Delete(path);
                return false;
            }
        }
        return true;
    }
}
