using ClusterManagement.Client.Management.Models;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// The cycle reports the owners in one scope watch, such as the <see cref="EngineStateTracker"/> of a page or the
/// <see cref="EngineCycleCollector"/>, keyed by engine host id for the whole chain and by deployment id for each engine.
/// The reports themselves are collected once for all of them by <see cref="EngineCycleHub"/>.
/// </summary>
/// <remarks>
/// Each owner watches on its own: the page that closes must not end the subscriptions of the page that opens in the
/// same circuit.
/// </remarks>
internal sealed partial class EngineCycleMonitor(IEngineHostBrokerProvider brokerProvider, EngineCycleHub hub,
    ILogger<EngineCycleMonitor> logger) : IAsyncDisposable
{
    private readonly Lock _ownersLock = new();
    private readonly HashSet<object> _owners = [];

    public CycleTimeSeries Series => hub.Series;

    /// <summary>
    /// Watches exactly these engine hosts for <paramref name="owner"/>, each on the broker its instance deployed it with.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the brokers could not be looked up; the engine hosts watched before then stay watched until a
    /// later call succeeds.
    /// </returns>
    public async Task<bool> Watch(object owner, IReadOnlyCollection<EngineHostRuntimeModel> engineHosts, CancellationToken cancellationToken)
    {
        if (await FindBrokers(engineHosts, cancellationToken) is not { } brokers)
            return false;

        lock (_ownersLock)
            _owners.Add(owner);

        await hub.Watch(owner, engineHosts
            .Where(host => brokers.ContainsKey(host.InstanceId))
            .ToDictionary(host => host.Id, host => brokers[host.InstanceId]), cancellationToken);
        return true;
    }

    /// <summary>
    /// Ends the subscriptions of <paramref name="owner"/>, for example when its page closes.
    /// </summary>
    public Task Stop(object owner)
    {
        lock (_ownersLock)
            _owners.Remove(owner);

        return hub.Release(owner);
    }

    public async ValueTask DisposeAsync()
    {
        object[] owners;
        lock (_ownersLock)
        {
            owners = [.. _owners];
            _owners.Clear();
        }

        foreach (var owner in owners)
            await hub.Release(owner);
    }

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
