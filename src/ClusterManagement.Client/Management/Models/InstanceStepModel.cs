using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Models;

public enum InstanceStepState
{
    Running,
    Succeeded,
    Failed,

    /// <summary>
    /// The engine host answered that it did not start or stop the engine; the instance went on.
    /// </summary>
    Refused,

    /// <summary>
    /// Still open when the instance's update ended or its rollback began, so its end will not come.
    /// </summary>
    Interrupted,
}

/// <summary>
/// A step of an instance's deployment update. The target is the engine of an engine step or the engine host of an
/// engine host step, by name or else by id; it is <c>null</c> for a step without one and for an engine the instance does
/// not know.
/// </summary>
public sealed class InstanceStepModel
{
    private readonly Guid? _engineHostId;
    private readonly Guid? _engineId;

    private InstanceStepModel(ClusterNodeDeploymentStepChanged change, bool isRollback)
    {
        Step = change.Step;
        _engineHostId = change.EngineHostId;
        _engineId = change.EngineId;
        Target = TargetOf(change);
        IsRollback = isRollback;
    }

    public ClusterNodeDeploymentStep Step { get; }
    public string? Target { get; }
    public bool IsRollback { get; }

    public InstanceStepState State { get; private set; }

    /// <summary>
    /// When the client received the step's start, to tell how long a running step has taken so far; <c>null</c> while
    /// the start has not arrived.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    // The instance's own times: a duration measured on one clock is right whatever that clock's offset, and also when
    // the events reach the client late and all at once.
    internal DateTimeOffset? StartedOnInstance { get; private set; }
    internal DateTimeOffset? EndedOnInstance { get; private set; }

    /// <summary>
    /// The error the instance reports for a failed step.
    /// </summary>
    public ErrorInfo? Error { get; private set; }

    public TimeSpan? Duration => EndedOnInstance - StartedOnInstance;

    internal static InstanceStepModel Start(ClusterNodeDeploymentStepChanged change, DateTimeOffset receivedAt, bool isRollback)
        => new(change, isRollback) { StartedAt = receivedAt, StartedOnInstance = change.OccurredAt, State = InstanceStepState.Running };

    /// <summary>
    /// A step whose end arrived before its start.
    /// </summary>
    internal static InstanceStepModel Unstarted(ClusterNodeDeploymentStepChanged change, bool isRollback)
        => new(change, isRollback);

    // By id, as two engines on different engine hosts can have the same name.
    internal bool Is(ClusterNodeDeploymentStepChanged change)
        => Step == change.Step && _engineHostId == change.EngineHostId && _engineId == change.EngineId;

    internal void StartedLate(ClusterNodeDeploymentStepChanged change, DateTimeOffset receivedAt)
    {
        StartedAt = receivedAt;
        StartedOnInstance = change.OccurredAt;
    }

    internal void End(ClusterNodeDeploymentStepChanged change)
    {
        EndedOnInstance = change.OccurredAt;
        State = change.Status switch
        {
            ClusterNodeDeploymentStepStatus.Failed => InstanceStepState.Failed,
            ClusterNodeDeploymentStepStatus.Refused => InstanceStepState.Refused,
            _ => InstanceStepState.Succeeded,
        };
        Error = change.Error;
    }

    internal void Interrupt()
    {
        if (State == InstanceStepState.Running)
            State = InstanceStepState.Interrupted;
    }

    private static string? TargetOf(ClusterNodeDeploymentStepChanged change)
        => IsAboutEngine(change.Step)
            ? change.EngineName ?? change.EngineId?.ToString()
            : change.EngineHostName ?? change.EngineHostId?.ToString();

    private static bool IsAboutEngine(ClusterNodeDeploymentStep step) => step is ClusterNodeDeploymentStep.StopEngine
        or ClusterNodeDeploymentStep.TearDownEngine
        or ClusterNodeDeploymentStep.DeployEngine
        or ClusterNodeDeploymentStep.StartEngine;
}
