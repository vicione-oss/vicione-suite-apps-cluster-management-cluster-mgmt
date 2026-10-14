using ClusterManagement.Public.Connections.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Deletes the certificate stores no OPC UA connection uses once the module started: those of connections that were
/// deleted while it was not running, and those of new connections whose settings dialog was cancelled after an upload.
/// </summary>
internal sealed partial class OpcUaCertificateCleanupService(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<OpcUaCertificateCleanupService> logger) : BackgroundService
{
    // a connection whose settings dialog is open may already have stores, but is not saved yet
    internal static TimeSpan OrphanedStoresMinAge { get; } = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // the connections are requested from another module, which has to be up
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
                await started.Task.WaitAsync(stoppingToken);

            using var scope = services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
            var response = await mediator.Request<GetConnections, GetConnectionsResponse>(
                new(null, [ConnectionType.OpcUaClient.Name, ConnectionType.OpcUaServer.Name], null), stoppingToken);

            // without the connections nothing is known to be unused
            if (response.RequestError is { } error)
            {
                LogGetConnectionsFailed(logger, error.Message);
                return;
            }

            scope.ServiceProvider.GetRequiredService<IOpcUaCertificateStore>()
                .CleanupOrphanedStores(response.Connections.SelectMany(OpcUaStorePaths.GetStorePaths), OrphanedStoresMinAge);
        }
        catch (OperationCanceledException)
        {
            // the module stopped before the cleanup
        }
        catch (Exception e)
        {
            // the next start tries again
            LogCleanupFailed(logger, e);
        }
    }

    [LoggerMessage(LogLevel.Warning, "Skipping the cleanup of OPC UA certificate stores, the connections could not be loaded: {Message}")]
    private static partial void LogGetConnectionsFailed(ILogger logger, string? message);

    [LoggerMessage(LogLevel.Error, "The cleanup of OPC UA certificate stores failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
