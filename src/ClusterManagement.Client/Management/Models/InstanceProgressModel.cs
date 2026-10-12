using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Models;

public sealed class InstanceProgressModel(Guid id, string name, string nodeName)
{
    private readonly List<InstanceStepModel> _steps = [];

    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string NodeName { get; } = nodeName;

    public bool IsStarted { get; private set; }
    public bool IsUpdated { get; private set; }
    public ErrorInfo? Error { get; private set; }

    public bool HasFailed => Error is not null;

    /// <summary>
    /// Whether the instance began its rollback, which runs its steps again to restore the previous version.
    /// </summary>
    public bool RollbackStarted { get; private set; }

    /// <summary>
    /// The steps of the update and its rollback in the order the client saw them start.
    /// </summary>
    public IReadOnlyList<InstanceStepModel> Steps => _steps;

    /// <summary>
    /// The steps of the rollback once it began, before that those of the update.
    /// </summary>
    public IReadOnlyList<InstanceStepModel> LatestRun => [.. _steps.Where(s => s.IsRollback == RollbackStarted)];

    /// <summary>
    /// The step in progress; <c>null</c> between steps and once the instance finished.
    /// </summary>
    public InstanceStepModel? CurrentStep => _steps.LastOrDefault(s => s.State == InstanceStepState.Running);

    /// <summary>
    /// The step that failed with <see cref="Error"/>. The update and its rollback can both fail, and the error shown
    /// can be either one, so the step is picked by its error; <c>null</c> when no step failed with it.
    /// </summary>
    public InstanceStepModel? FailedStep => Error is null
        ? null
        : _steps.LastOrDefault(s => s.State == InstanceStepState.Failed && s.Error == Error);

    /// <summary>
    /// From the first start to the last end of the <see cref="LatestRun"/>, by the instance's clock; <c>null</c> until
    /// one of its steps ended.
    /// </summary>
    public TimeSpan? Duration
    {
        get
        {
            var run = LatestRun;
            return run.Max(s => s.EndedOnInstance) - run.Min(s => s.StartedOnInstance);
        }
    }

    internal void Record(ClusterUpdateChange change, ErrorInfo? error)
    {
        if (error is not null)
        {
            Fail(error);
            return;
        }

        IsStarted |= change == ClusterUpdateChange.Start;
        IsUpdated |= change == ClusterUpdateChange.Update;
        if (IsUpdated)
            InterruptRunningSteps();
    }

    // The events of one instance can arrive out of order: an end closes the open step it belongs to, and a start that
    // arrives after its end completes that step instead of opening it again.
    internal void Record(ClusterNodeDeploymentStepChanged change, DateTimeOffset receivedAt, bool rollingBack)
    {
        if (!RollbackStarted && BeginsRollback(change, rollingBack))
        {
            RollbackStarted = true;
            InterruptRunningSteps();
        }

        if (change.Status == ClusterNodeDeploymentStepStatus.Started)
        {
            if (_steps.LastOrDefault(s => s.Is(change) && s.StartedAt is null) is { } ended)
                ended.StartedLate(change, receivedAt);
            else
                _steps.Add(InstanceStepModel.Start(change, receivedAt, RollbackStarted));
            return;
        }

        var step = _steps.LastOrDefault(s => s.Is(change) && s.EndedOnInstance is null);
        if (step is null)
        {
            step = InstanceStepModel.Unstarted(change, RollbackStarted);
            _steps.Add(step);
        }

        step.End(change);
    }

    // The rollback runs the instance's steps again from the start, and the saga sends it before it reports the rollback
    // phase, so the rollback begins with the second start of the first step. A preparation whose end arrived first is
    // still the update's own; a client that saw no step of the update goes by the phase.
    private bool BeginsRollback(ClusterNodeDeploymentStepChanged change, bool rollingBack)
        => _steps.Count == 0
            ? rollingBack
            : change is { Step: ClusterNodeDeploymentStep.Prepare, Status: ClusterNodeDeploymentStepStatus.Started }
                && !_steps.Any(s => s.Is(change) && s.StartedAt is null);

    internal void Fail(ErrorInfo error)
    {
        Error = error;
        IsUpdated = false;
        InterruptRunningSteps();
    }

    internal void InterruptRunningSteps()
    {
        foreach (var step in _steps)
            step.Interrupt();
    }

    /// <summary>
    /// Interrupts the running steps of the update, but not those of a rollback that already reported its first step.
    /// </summary>
    internal void InterruptRunningUpdateSteps()
    {
        foreach (var step in _steps.Where(s => !s.IsRollback))
            step.Interrupt();
    }
}
