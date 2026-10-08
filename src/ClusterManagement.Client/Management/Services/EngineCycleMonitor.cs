using ClusterManagement.Client.Management.Models;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// The cycle reports one page, or the <see cref="EngineCycleCollector"/>, watches, keyed by engine host id for the whole
/// chain and by deployment id for each engine. The reports themselves are collected once for all of them by
/// <see cref="EngineCycleHub"/>.
/// </summary>
internal sealed partial class EngineCycleMonitor(IEngineHostBrokerProvider brokerProvider, EngineCycleHub hub,
    ILogger<EngineCycleMonitor> logger) : IAsyncDisposable
{
    public CycleTimeSeries Series => hub.Series;

    /// <summary>
    /// Watches exactly these engine hosts, each on the broker its instance deployed it with.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the brokers could not be looked up; the engine hosts watched before then stay watched until a
    /// later call succeeds.
    /// </returns>
    public async Task<bool> Watch(IReadOnlyCollection<EngineHostRuntimeModel> engineHosts, CancellationToken cancellationToken)
    {
        if (await FindBrokers(engineHosts, cancellationToken) is not { } brokers)
            return false;

        await hub.Watch(this, engineHosts
            .Where(host => brokers.ContainsKey(host.InstanceId))
            .ToDictionary(host => host.Id, host => brokers[host.InstanceId]), cancellationToken);
        return true;
    }

    /// <summary>
    /// Ends the subscriptions of this page, for example when it closes.
    /// </summary>
    public Task Stop()
        => hub.Release(this);

    public async ValueTask DisposeAsync()
        => await hub.Release(this);

    private async Task<IReadOnlyDictionary<Guid, EngineHostBroker>?> FindBrokers(IEnumerable<EngineHostRuntimeModel> engineHosts,
        CancellationToken cancellationToken)
    {
        try
        {
            return await brokerProvider.FindBrokers([.. engineHosts.Select(h => h.InstanceId).Distinct()], cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFindBrokersFailed(logger, e);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to find the brokers of the engine hosts")]
    private static partial void LogFindBrokersFailed(ILogger logger, Exception exception);
}
