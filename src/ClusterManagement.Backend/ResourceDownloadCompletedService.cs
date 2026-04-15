using ClusterManagement.Public.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend;

internal sealed partial class ResourceDownloadCompletedService(
    IServiceProvider services,
    ILogger<ResourceDownloadCompletedService> logger) : BackgroundService
{
    private static bool s_completed;

    internal static bool IsCompleted => s_completed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var downloaders = services.GetServices<IHostedService>().OfType<IDownloader>();
            await Task.WhenAll(downloaders.Select(d => d.Task.WaitAsync(stoppingToken)));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            LogDownloadError(logger, ex);
        }

        s_completed = true;

        LogDownloadCompleted(logger);

        using var scope = services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
        await mediator.Publish(new ResourcesDownloaded(), stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "One or more resource downloads failed")]
    private static partial void LogDownloadError(ILogger<ResourceDownloadCompletedService> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "All resource downloads completed. Publishing ResourcesDownloaded event.")]
    private static partial void LogDownloadCompleted(ILogger<ResourceDownloadCompletedService> logger);
}
