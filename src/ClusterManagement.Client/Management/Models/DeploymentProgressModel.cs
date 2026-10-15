using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Extensions;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Models;

public sealed class DeploymentProgressModel
{
    private static readonly DeploymentPhase[] s_forwardSteps =
    [
        DeploymentPhase.Committing,
        DeploymentPhase.AddingNodes,
        DeploymentPhase.UpdatingDeployments,
        DeploymentPhase.CleaningUp,
        DeploymentPhase.Completed,
    ];

    /// <summary>
    /// The steps of a deployment that ran through every phase.
    /// </summary>
    public static IReadOnlyList<DeploymentStep> CompletedSteps { get; } =
        [.. s_forwardSteps.Select(phase => new DeploymentStep(phase, DeploymentStepState.Done))];

    private readonly Dictionary<Guid, string> _names = [];

    private DeploymentProgressModel(Guid clusterId, Guid correlationId, Version targetVersion, DateTimeOffset? startedAt,
        IReadOnlyList<InstanceProgressModel> instances)
    {
        ClusterId = clusterId;
        CorrelationId = correlationId;
        TargetVersion = targetVersion;
        StartedAt = startedAt;
        Instances = instances;
    }

    public Guid ClusterId { get; }

    /// <summary>
    /// The deployment saga's, which the update and its rollback share.
    /// </summary>
    public Guid CorrelationId { get; }

    public Version TargetVersion { get; }

    /// <summary>
    /// <c>null</c> when the deployment was already running before it was observed, or failed before it started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; }

    public IReadOnlyList<InstanceProgressModel> Instances { get; }

    public DeploymentPhase Phase { get; private set; } = DeploymentPhase.Starting;

    /// <summary>
    /// The last phase reached before a rollback started.
    /// </summary>
    public DeploymentPhase ForwardPhase { get; private set; } = DeploymentPhase.Starting;

    /// <summary>
    /// The phase the saga was in when the deployment failed.
    /// </summary>
    public DeploymentPhase? FailedPhase { get; private set; }

    public string? FailureReason { get; private set; }
    public TimeSpan? Duration { get; private set; }

    public bool IsRunning => Phase is not (DeploymentPhase.Completed or DeploymentPhase.Failed);
    public bool HasRolledBack { get; private set; }
    public bool RollbackFailed => FailedPhase == DeploymentPhase.RollingBack && !WasReset;

    /// <summary>
    /// The deployment state was reset: the deployment was abandoned in <see cref="FailedPhase"/>, where its steps stop,
    /// and did not fail there.
    /// </summary>
    public bool WasReset { get; private set; }

    /// <summary>
    /// The forward phase that failed; a failed rollback points back to the phase it tried to undo.
    /// </summary>
    public DeploymentPhase? FailurePhase => FailedPhase == DeploymentPhase.RollingBack ? ForwardPhase : FailedPhase;

    public static DeploymentProgressModel Start(ClusterUpdateStarted started, DateTimeOffset startedAt)
    {
        var instances = started.NodeApplications
            .Where(k => k.Value is not null)
            .Select(k => new InstanceProgressModel(k.Value!.Value, started.Applications[k.Value.Value], started.Nodes[k.Key]))
            .ToList();

        return new(started.ClusterId, started.CorrelationId, started.TargetVersion, startedAt, instances);
    }

    /// <summary>
    /// Picks up a deployment that is running while the page loads. The instances are those of
    /// <paramref name="target"/>, one per node as the deployment lists them; without it the list stays empty.
    /// </summary>
    public static DeploymentProgressModel Resume(DeploymentState state, Cluster? target)
    {
        var instances = target is null ? [] : InstancesOf(target);
        var progress = new DeploymentProgressModel(state.ClusterId, state.CorrelationId, state.TargetVersion, null, instances);
        progress.EnterPhase(DeploymentPhases.FromSagaState(state.CurrentState));
        return progress;
    }

    /// <summary>
    /// A deployment that failed before it started, for example because a version could not be loaded; the saga reports
    /// neither its start nor its instances.
    /// </summary>
    public static DeploymentProgressModel FailedBeforeStart(ClusterUpdateFailed failed)
    {
        var progress = new DeploymentProgressModel(failed.ClusterId, failed.CorrelationId, failed.Version, null, []);
        progress.Fail(failed);
        return progress;
    }

    // Mirrors the deployment: the first CoreOS application of each node is the instance it updates.
    private static List<InstanceProgressModel> InstancesOf(Cluster cluster)
        => [.. cluster.NodeGroups
            .SelectMany(group => group.Nodes)
            .Select(node => (Node: node, Instance: node.Applications.FirstOrDefault(a => a.IsCoreOs())))
            .Where(n => n.Instance is not null)
            .Select(n => new InstanceProgressModel(n.Instance!.Id, n.Instance.Name, n.Node.Name))];

    /// <summary>
    /// Names the engine hosts and engines of <paramref name="cluster"/> in the steps that carry only their ids. An
    /// instance names what the version it deploys holds, so pass both versions: the update removes what only the version
    /// that ran before holds, and the rollback what only the target version holds. A name known first is kept.
    /// </summary>
    public void KnowNamesOf(Cluster cluster)
    {
        foreach (var engineHost in cluster.NodeGroups.SelectMany(g => g.Nodes).SelectMany(n => n.Applications).SelectMany(a => a.EngineHosts))
        {
            _names.TryAdd(engineHost.Id, engineHost.Name);
            foreach (var engine in engineHost.Engines)
                _names.TryAdd(engine.Id, engine.Name);
        }
    }

    public bool Matches(Guid clusterId, Version version)
        => ClusterId == clusterId && TargetVersion == version;

    public bool Matches(Guid clusterId, Guid correlationId)
        => ClusterId == clusterId && CorrelationId == correlationId;

    public void Apply(ClusterUpdateChanged change)
    {
        if (change.Change == ClusterUpdateChange.Transition)
        {
            EnterPhase(DeploymentPhases.FromSagaState(change.CurrentState));
            return;
        }

        var instance = Instances.FirstOrDefault(k => k.Id == change.ApplicationId);
        instance?.Record(change.Change, change.Error);
    }

    /// <summary>
    /// Records a step of one of the instances; <paramref name="receivedAt"/> is when the client received it.
    /// </summary>
    public void Apply(ClusterNodeDeploymentStepChanged step, DateTimeOffset receivedAt)
    {
        var instance = Instances.FirstOrDefault(k => k.Id == step.ClusterApplicationId);
        instance?.Record(Named(step), receivedAt, Phase == DeploymentPhase.RollingBack);
    }

    private ClusterNodeDeploymentStepChanged Named(ClusterNodeDeploymentStepChanged step) => step with
    {
        EngineHostName = step.EngineHostName ?? NameOf(step.EngineHostId),
        EngineName = step.EngineName ?? NameOf(step.EngineId),
    };

    private string? NameOf(Guid? id)
        => id is { } key && _names.TryGetValue(key, out var name) ? name : null;

    public void Complete(ClusterUpdateCompleted completed)
    {
        Phase = DeploymentPhase.Completed;
        Duration = completed.Duration;
    }

    // A deployment that rolled back ends in a rollback state whether the rollback worked or not. The saga clears the
    // errors of the forward run before it rolls back, so only errors the instances reported on the way back, or a failed
    // version it could not delete, mean it failed.
    public void Fail(ClusterUpdateFailed failed)
    {
        var failedPhase = DeploymentPhases.FromSagaState(failed.State) ?? (HasRolledBack ? DeploymentPhase.RollingBack : Phase);
        WasReset = failed.WasReset;
        FailedPhase = failedPhase == DeploymentPhase.RollingBack && failed.ErrorsByApplication.Count == 0 && !failed.FailedVersionKept && !WasReset
            ? ForwardPhase
            : failedPhase;
        Phase = DeploymentPhase.Failed;
        FailureReason = failed.Reason;
        Duration = failed.Duration;

        foreach (var (applicationId, error) in failed.ErrorsByApplication)
            Instances.FirstOrDefault(k => k.Id == applicationId)?.Fail(error);
    }

    public IReadOnlyList<DeploymentStep> GetSteps()
    {
        var steps = s_forwardSteps.Select(ToForwardStep).ToList();
        if (HasRolledBack)
            steps.Add(new DeploymentStep(DeploymentPhase.RollingBack, RollbackState()));

        return steps;
    }

    private DeploymentStep ToForwardStep(DeploymentPhase step)
    {
        if (Phase == DeploymentPhase.Completed)
            return new DeploymentStep(step, DeploymentStepState.Done);

        var reached = Array.IndexOf(s_forwardSteps, ForwardPhase);
        var index = Array.IndexOf(s_forwardSteps, step);
        var stoppedHere = Phase is DeploymentPhase.Failed or DeploymentPhase.RollingBack;

        var state = index < reached ? DeploymentStepState.Done
            : index > reached ? DeploymentStepState.Pending
            : stoppedHere ? DeploymentStepState.Failed
            : DeploymentStepState.Current;

        return new DeploymentStep(step, state);
    }

    private DeploymentStepState RollbackState()
    {
        if (Phase == DeploymentPhase.RollingBack)
            return DeploymentStepState.Current;

        return RollbackFailed || WasReset ? DeploymentStepState.Failed : DeploymentStepState.Done;
    }

    private void EnterPhase(DeploymentPhase? phase)
    {
        if (phase is null)
            return;

        // The rollback runs the instances' steps again; a step the update left behind is not one of them.
        if (phase == DeploymentPhase.RollingBack && Phase != DeploymentPhase.RollingBack)
        {
            foreach (var instance in Instances)
                instance.InterruptRunningUpdateSteps();
        }

        Phase = phase.Value;
        HasRolledBack |= phase == DeploymentPhase.RollingBack;
        if (phase != DeploymentPhase.RollingBack)
            ForwardPhase = phase.Value;
    }
}
