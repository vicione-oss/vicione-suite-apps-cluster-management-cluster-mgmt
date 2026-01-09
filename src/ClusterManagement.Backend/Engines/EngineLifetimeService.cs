using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal sealed class EngineLifetimeService(IServiceProvider services) : IHostedLifecycleService
{
    private readonly IServiceProvider _services = services;

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EngineLifetimeService>>();

        try
        {
            var downloader = scope.ServiceProvider.GetServices<IHostedService>().OfType<IDownloader>();
            await Task.WhenAll(downloader.Select(d => d.Task));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute downloads");
            return;
        }

        try
        {
            var engineLifetime = scope.ServiceProvider.GetRequiredService<EngineLifetime>();
            await engineLifetime.Start(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start engines automatically");
        }
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var engineLifetime = scope.ServiceProvider.GetRequiredService<EngineLifetime>();
        await engineLifetime.Stop(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
