using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// Watches the engine hosts of the running cluster version from startup on, so the runtime panel opens with the cycles
/// of the last minute instead of an empty chart. Only the running version is watched; after each deployment the watch
/// moves to the version that runs then, and without a running version no broker is connected.
/// </summary>
internal sealed partial class EngineCycleCollector(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<EngineCycleCollector> logger) : IEventConsumer<ClusterUpdateCompleted>, IAsyncDisposable
{
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private AsyncServiceScope _scope;
    private IDisposable? _subscription;
    private int _loadGeneration;

    /// <summary>
    /// Starts watching in the background; the backend may not answer yet while the suite starts, so a failed load is
    /// retried.
    /// </summary>
    public void Start()
    {
        _scope = scopeFactory.CreateAsyncScope();
        _subscription = _scope.ServiceProvider.GetRequiredService<IUiMediator>().Register(this);

        _ = LoadUntilWatched(_lifetime.Token);
    }

    // The event is delivered to every consumer in turn, so the load must not hold it up.
    public Task Consume(ClientContext<ClusterUpdateCompleted> context, CancellationToken cancellationToken)
    {
        _ = LoadUntilWatched(_lifetime.Token);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        _subscription?.Dispose();
        await _scope.DisposeAsync();
        _loadLock.Dispose();
        _lifetime.Dispose();
    }

    // A newer load, started by a later deployment, ends the retries of an older one.
    private async Task LoadUntilWatched(CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            while (generation == Volatile.Read(ref _loadGeneration) && !await TryWatchRunningVersion(cancellationToken))
                await Task.Delay(s_retryDelay, timeProvider, cancellationToken);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The collector was disposed.
        }
    }

    private async Task<bool> TryWatchRunningVersion(CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            var services = _scope.ServiceProvider;
            var engineHosts = await RunningEngineHosts(services.GetRequiredService<IClusterService>(), cancellationToken);
            var monitor = services.GetRequiredService<EngineCycleMonitor>();
            if (engineHosts.Count > 0)
                return await monitor.Watch(engineHosts, cancellationToken);

            await monitor.Stop();
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogLoadFailed(logger, e);
            return false;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static async Task<IReadOnlyList<EngineHostRuntimeModel>> RunningEngineHosts(IClusterService clusters, CancellationToken cancellationToken)
    {
        var running = (await clusters.QueryClusterInfos(null, cancellationToken)).FirstOrDefault(info => info.IsActive);
        return running is null
            ? []
            : EngineHostRuntimeModel.FromCluster(await clusters.Query(running.Id, running.Version, cancellationToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load the running cluster version to collect its engine cycles")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);
}
