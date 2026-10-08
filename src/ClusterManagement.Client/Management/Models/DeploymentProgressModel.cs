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

    private DeploymentProgressModel(Guid clusterId, Version targetVersion, DateTimeOffset? startedAt, IReadOnlyList<InstanceProgressModel> instances)
    {
        ClusterId = clusterId;
        TargetVersion = targetVersion;
        StartedAt = startedAt;
        Instances = instances;
    }

    public Guid ClusterId { get; }
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
    public bool RollbackFailed => FailedPhase == DeploymentPhase.RollingBack;

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

        return new(started.ClusterId, started.TargetVersion, startedAt, instances);
    }

    /// <summary>
    /// Picks up a deployment that is running while the page loads. The instances are those of
    /// <paramref name="target"/>, one per node as the deployment lists them; without it the list stays empty.
    /// </summary>
    public static DeploymentProgressModel Resume(DeploymentState state, Cluster? target)
    {
        var instances = target is null ? [] : InstancesOf(target);
        var progress = new DeploymentProgressModel(state.ClusterId, state.TargetVersion, null, instances);
        progress.EnterPhase(DeploymentPhases.FromSagaState(state.CurrentState));
        return progress;
    }

    /// <summary>
    /// A deployment that failed before it started, for example because a version could not be loaded; the saga reports
    /// neither its start nor its instances.
    /// </summary>
    public static DeploymentProgressModel FailedBeforeStart(ClusterUpdateFailed failed)
    {
        var progress = new DeploymentProgressModel(failed.ClusterId, failed.Version, null, []);
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

    public bool Matches(Guid clusterId, Version version)
        => ClusterId == clusterId && TargetVersion == version;

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

    public void Complete(ClusterUpdateCompleted completed)
    {
        Phase = DeploymentPhase.Completed;
        Duration = completed.Duration;
    }

    public void Fail(ClusterUpdateFailed failed)
    {
        FailedPhase = DeploymentPhases.FromSagaState(failed.State) ?? (HasRolledBack ? DeploymentPhase.RollingBack : Phase);
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

        return RollbackFailed ? DeploymentStepState.Failed : DeploymentStepState.Done;
    }

    private void EnterPhase(DeploymentPhase? phase)
    {
        if (phase is null)
            return;

        Phase = phase.Value;
        HasRolledBack |= phase == DeploymentPhase.RollingBack;
        if (phase != DeploymentPhase.RollingBack)
            ForwardPhase = phase.Value;
    }
}
