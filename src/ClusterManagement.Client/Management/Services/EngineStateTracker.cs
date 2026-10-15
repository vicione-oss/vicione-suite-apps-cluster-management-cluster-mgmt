using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// Keeps the state of the engines of a cluster version current for the component that owns it. It asks every engine's
/// instance at once and takes each answer as it arrives, asks an engine whose host did not answer again a few seconds
/// after each failed answer, and asks the engines of a host again when its cycle reports stop or resume, since neither
/// raises an engine event.
/// </summary>
/// <remarks>
/// Each owner creates its own, since it lives as long as its owner and runs on the owner's renderer: <c>invokeAsync</c>
/// brings timers and events onto it, and <c>changed</c> tells the owner that something it shows changed.
/// </remarks>
internal sealed partial class EngineStateTracker(
    IUiMediator mediator,
    EngineCycleMonitor cycleMonitor,
    TimeProvider timeProvider,
    ILogger logger,
    Func<Func<Task>, Task> invokeAsync,
    Action changed) : IAsyncDisposable
{
    private static readonly TimeSpan s_stateTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_minimumSilence = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_longestDeploymentHold = TimeSpan.FromMinutes(2);

    private readonly CancellationTokenSource _tokenSource = new();

    // Several queries for one engine can overlap (a retry, an engine event, a reload); only the newest one counts.
    private readonly Dictionary<Guid, int> _latestQueries = [];

    // An engine whose host does not answer has one retry pending at a time, however many events arrive for it meanwhile.
    private readonly HashSet<(Guid EngineId, int Generation)> _pendingRetries = [];

    // Whether each engine host sent cycle reports at the last check; a change asks its engines again.
    private readonly Dictionary<Guid, bool> _reporting = [];

    private int _reloadGeneration;

    // The deployment that holds the engines, by its correlation id; null while none runs.
    private Guid? _deployment;

    // While a deployment runs, no engine is asked until then; null once the hold is over or no deployment runs.
    private DateTimeOffset? _holdEnds;

    /// <summary>
    /// The engine hosts with their engines, each engine with its latest known state.
    /// </summary>
    public IReadOnlyList<EngineHostRuntimeModel> Hosts { get; private set; } = [];

    /// <summary>
    /// When every engine of <see cref="Hosts"/> answered or failed to answer for the first time; <c>null</c> before.
    /// </summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    public CycleTimeSeries Series => cycleMonitor.Series;

    /// <summary>
    /// Whether a deployment of the cluster runs. It replaces the engines and may restart their hosts, and the backend
    /// waits for a restarting host up to its MQTT timeout (100 s by default), so no engine is asked meanwhile. A
    /// deployment state that hangs must not hide the engines for good, so two minutes after the deployment last reported
    /// progress they are asked anyway.
    /// </summary>
    public bool IsDeploying => _deployment is not null && _holdEnds is not null;

    /// <summary>
    /// Follows <paramref name="hosts"/> from now on, in place of those followed before; while
    /// <paramref name="deployment"/> runs, their engines are asked only once it ends.
    /// </summary>
    /// <remarks>
    /// Never ends a deployment: one that started while the owner loaded <paramref name="hosts"/> is newer than what it
    /// loaded, so only <see cref="SetDeployment"/> or <see cref="EndDeployment"/> ends it.
    /// </remarks>
    public async Task Track(IReadOnlyList<EngineHostRuntimeModel> hosts, Guid? deployment = null)
    {
        Hosts = hosts;
        CheckedAt = null;
        _reporting.Clear();
        if (deployment is { } running && running != _deployment)
            HoldForDeployment(running);

        // A broker that is slow to connect must not hold back the engine states.
        await Task.WhenAll(cycleMonitor.Watch(this, Hosts, _tokenSource.Token), ReloadStates());
    }

    /// <summary>
    /// Asks one engine again, for example after an engine event; the others keep their state.
    /// </summary>
    /// <remarks>
    /// Does not wait for the answer: the UI events of an instance arrive one at a time on Standalone, and an engine host
    /// that does not answer would hold back every other UI event until the request times out.
    /// </remarks>
    public void Reload(Guid engineId)
        => _ = invokeAsync(() => ReloadEngine(engineId));

    /// <summary>
    /// Holds the questions while <paramref name="deployment"/> runs, and asks every engine again once none runs. Another
    /// deployment holds them anew, also after a hung one held them for as long as any deployment may; the same one does
    /// not.
    /// </summary>
    public async Task SetDeployment(Guid? deployment)
    {
        if (_deployment == deployment || _tokenSource.IsCancellationRequested)
            return;

        if (deployment is { } running)
            HoldForDeployment(running);
        else
            (_deployment, _holdEnds) = (null, null);

        changed();

        if (deployment is null && Hosts.Count > 0)
            await ReloadStates();
    }

    /// <summary>
    /// Renews the hold while <paramref name="deployment"/> reports progress, so only a deployment that stops reporting
    /// counts as hung; once the hold is over, nothing renews it.
    /// </summary>
    public void DeploymentAlive(Guid deployment)
    {
        if (_deployment == deployment && _holdEnds is not null)
            _holdEnds = timeProvider.GetUtcNow() + s_longestDeploymentHold;
    }

    /// <summary>
    /// Ends <paramref name="deployment"/> if it is the one that holds the engines; the end of another, such as a hung
    /// deployment whose state was reset while a newer one runs, keeps them held.
    /// </summary>
    public Task EndDeployment(Guid deployment)
        => _deployment == deployment ? SetDeployment(null) : Task.CompletedTask;

    /// <summary>
    /// Asks the engines of each host again whose cycle reports stopped or resumed since the last check, and every engine
    /// once a deployment held them longer than any deployment should take.
    /// </summary>
    public void CheckReporting(DateTimeOffset now)
    {
        if (_holdEnds is { } holdEnds && now >= holdEnds)
        {
            _holdEnds = null;
            LogDeploymentHoldEnded(logger, s_longestDeploymentHold.TotalMinutes);
            _ = ReloadStates();
        }

        foreach (var host in Hosts)
        {
            // A deployment restarts hosts; the reload at its end asks every engine anyway.
            var reporting = IsReporting(host, now);
            if (!IsDeploying && _reporting.TryGetValue(host.Id, out var wasReporting) && wasReporting != reporting)
            {
                foreach (var engine in host.Engines)
                    _ = ReloadState(engine, _reloadGeneration);
            }

            _reporting[host.Id] = reporting;
        }
    }

    /// <summary>
    /// A host reports every cycle; three cycles without a report, and at least a few seconds, count as silence.
    /// </summary>
    public bool IsReporting(EngineHostRuntimeModel host, DateTimeOffset now)
    {
        var silence = TimeSpan.FromMilliseconds(Math.Max(s_minimumSilence.TotalMilliseconds, 3.0 * host.CycleTime));
        return Series.LastReport(host.Id) is { } lastReport && now - lastReport <= silence;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        await cycleMonitor.Stop(this);
    }

    private async Task ReloadStates()
    {
        // A slower reload for previous hosts must not overwrite the current ones.
        var generation = ++_reloadGeneration;
        changed();

        // Nothing is asked while a deployment holds the engines, so they are not checked either.
        if (IsDeploying)
            return;

        await Task.WhenAll(Hosts.SelectMany(h => h.Engines).Select(engine => ReloadState(engine, generation)));
        if (generation != _reloadGeneration || _tokenSource.IsCancellationRequested)
            return;

        CheckedAt = timeProvider.GetUtcNow();
        changed();
    }

    private async Task ReloadEngine(Guid engineId)
    {
        var engine = Hosts.SelectMany(h => h.Engines).FirstOrDefault(e => e.EngineId == engineId);
        if (engine is not null)
            await ReloadState(engine, _reloadGeneration);
    }

    // Each state is shown as soon as it arrives: an instance that does not answer would otherwise hold back every
    // other engine until its request times out.
    // Work already on its way when the owner closed, such as an engine event, asks nothing.
    private async Task ReloadState(EngineRuntimeModel engine, int generation)
    {
        if (IsDeploying || _tokenSource.IsCancellationRequested)
            return;

        var query = _latestQueries[engine.EngineId] = _latestQueries.GetValueOrDefault(engine.EngineId) + 1;
        if (await QueryState(engine) is not { } checkedEngine
            || generation != _reloadGeneration
            || query != _latestQueries[engine.EngineId])
        {
            return;
        }

        Hosts = EngineHostRuntimeModel.WithEngine(Hosts, checkedEngine);
        changed();

        if (checkedEngine is { IsReachable: false, IsDeployed: true } && _pendingRetries.Add((checkedEngine.EngineId, generation)))
            _ = AskAgainLater(checkedEngine.EngineId, generation, _tokenSource.Token);
    }

    // An engine host that was restarting when it was asked answers again later, often without an engine event.
    private async Task AskAgainLater(Guid engineId, int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_stateTimeout, timeProvider, cancellationToken);
            await invokeAsync(async () =>
            {
                _pendingRetries.Remove((engineId, generation));
                if (generation == _reloadGeneration)
                    await ReloadEngine(engineId);
            });
        }
        catch (OperationCanceledException)
        {
            // The owner was disposed.
        }
    }

    // An engine host that is restarted while it is asked may never answer; the owner must not wait for it forever, and
    // the instance stops asking it as well. Returns null once the owner is gone, so a late answer changes nothing.
    private async Task<EngineRuntimeModel?> QueryState(EngineRuntimeModel engine)
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
        try
        {
            var response = await mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
                    new(engine.EngineId), engine.InstanceId, request.Token)
                .WaitAsync(s_stateTimeout, timeProvider, _tokenSource.Token);

            // An instance without a deployment of the engine answered as well.
            var isDeployed = response.RequestError?.ErrorCode != ClusterErrors.EngineNotDeployed;
            if (response.RequestError is { } error && isDeployed && engine is not { IsChecked: true, IsReachable: false })
                LogEngineStateRefused(logger, engine.EngineId, error.ErrorCode, error.Message);

            return engine with
            {
                IsChecked = true,
                State = response.EngineState,
                IsReachable = response.RequestError is null || !isDeployed,
                IsDeployed = isDeployed,
                DeploymentId = response.DeploymentId ?? engine.DeploymentId,
            };
        }
        catch (Exception) when (_tokenSource.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await request.CancelAsync();

            // An engine host that stays unreachable is asked again and again; only the change is worth a warning.
            if (engine is { IsChecked: true, IsReachable: false })
                LogEngineStateErrorAgain(logger, e, engine.EngineId);
            else
                LogEngineStateError(logger, e, engine.EngineId);

            return engine with { IsChecked = true, State = null, IsReachable = false };
        }
    }

    private void HoldForDeployment(Guid deployment)
        => (_deployment, _holdEnds) = (deployment, timeProvider.GetUtcNow() + s_longestDeploymentHold);

    [LoggerMessage(Level = LogLevel.Information, Message = "A deployment reported no progress for {Minutes} minutes; the engines are asked anyway")]
    private static partial void LogDeploymentHoldEnded(ILogger logger, double minutes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get the state of engine {EngineId}")]
    private static partial void LogEngineStateError(ILogger logger, Exception exception, Guid engineId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The instance of engine {EngineId} could not get its state: {ErrorCode} {ErrorMessage}")]
    private static partial void LogEngineStateRefused(ILogger logger, Guid engineId, int errorCode, string? errorMessage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed again to get the state of engine {EngineId}")]
    private static partial void LogEngineStateErrorAgain(ILogger logger, Exception exception, Guid engineId);
}
