using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Models;

public sealed class InstanceProgressModel(Guid id, string name, string nodeName)
{
    private readonly List<InstanceStepModel> _steps = [];
    private IReadOnlyList<ClusterNodeDeploymentPlannedStep> _updatePlan = [];
    private IReadOnlyList<ClusterNodeDeploymentPlannedStep> _rollbackPlan = [];

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
    /// The <see cref="Steps"/> followed by the planned ones the latest run has not reached yet, as
    /// <see cref="InstanceStepState.Pending"/>; without a plan, or once the update succeeded or failed, the
    /// <see cref="Steps"/> alone. The instance reports no end of its rollback, so a rollback keeps its pending steps.
    /// </summary>
    public IReadOnlyList<InstanceStepModel> Timeline
        => !RollbackStarted && (IsUpdated || HasFailed) ? Steps : [.. _steps, .. NotReached()];

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
    internal void Record(ClusterNodeDeploymentStepChanged change, DateTimeOffset receivedAt)
    {
        if (change.IsRollback)
            BeginRollback();

        if (change.Status == ClusterNodeDeploymentStepStatus.Started)
        {
            if (_steps.LastOrDefault(s => s.Is(change) && s.StartedAt is null) is { } ended)
                ended.StartedLate(change, receivedAt);
            else
                _steps.Add(InstanceStepModel.Start(change, receivedAt, change.IsRollback));
            return;
        }

        var step = _steps.LastOrDefault(s => s.Is(change) && s.EndedOnInstance is null);
        if (step is null)
        {
            step = InstanceStepModel.Unstarted(change, change.IsRollback);
            _steps.Add(step);
        }

        step.End(change);
        RollbackFailed |= change is { IsRollback: true, Status: ClusterNodeDeploymentStepStatus.Failed };
    }

    /// <summary>
    /// Takes the steps the instance expects its update or its rollback to take; a later plan of the same run replaces
    /// the earlier one, and a plan of the rollback begins it.
    /// </summary>
    internal void Plan(IReadOnlyList<ClusterNodeDeploymentPlannedStep> planned, bool isRollback)
    {
        if (isRollback)
        {
            _rollbackPlan = planned;
            BeginRollback();
        }
        else
        {
            _updatePlan = planned;
        }
    }

    // A step of the update still running once the rollback began counts as interrupted unless its end still arrives.
    private void BeginRollback()
    {
        if (RollbackStarted)
            return;

        RollbackStarted = true;
        InterruptRunningSteps();
    }

    /// <summary>
    /// How many steps the latest run plans to take; 0 until its plan arrived.
    /// </summary>
    public int PlannedCount => (RollbackStarted ? _rollbackPlan : _updatePlan).Count;

    /// <summary>
    /// A step of the instance's rollback failed, or the deployment ended with an error of it from the rollback.
    /// </summary>
    public bool RollbackFailed { get; private set; }

    /// <summary>
    /// Whether the client saw the latest run from the start of its first planned step on, so its steps and their duration
    /// are the whole run; without a plan the client cannot tell and counts it as seen.
    /// </summary>
    public bool SawRunFromStart
    {
        get
        {
            var plan = RollbackStarted ? _rollbackPlan : _updatePlan;
            var run = LatestRun;
            return plan.Count == 0 || (run.Count > 0 && run[0].Is(plan[0]) && run[0].StartedOnInstance is not null);
        }
    }

    /// <summary>
    /// How many planned steps of the latest run lie behind it, counted by the place in the plan of the last step it
    /// reported, so steps the client did not see, as on a page opened during the deployment, count as well. A step in
    /// progress does not count yet.
    /// </summary>
    public int PlannedReached
    {
        get
        {
            var (next, last) = PlanPosition();
            return last?.State == InstanceStepState.Running ? next - 1 : next;
        }
    }

    // The planned steps after the last one the run reported; a planned step it skipped is left out.
    private IEnumerable<InstanceStepModel> NotReached()
        => (RollbackStarted ? _rollbackPlan : _updatePlan).Skip(PlanPosition().Next)
            .Select(planned => InstanceStepModel.Pending(planned, RollbackStarted));

    // The place in the plan after the last reported step of the latest run that the plan holds, and that step.
    private (int Next, InstanceStepModel? Last) PlanPosition()
    {
        var plan = RollbackStarted ? _rollbackPlan : _updatePlan;
        var next = 0;
        InstanceStepModel? last = null;
        foreach (var step in LatestRun)
        {
            var index = IndexOf(plan, step, next);
            if (index >= 0)
            {
                next = index + 1;
                last = step;
            }
        }

        return (next, last);
    }

    private static int IndexOf(IReadOnlyList<ClusterNodeDeploymentPlannedStep> plan, InstanceStepModel step, int from)
    {
        for (var i = from; i < plan.Count; i++)
        {
            if (step.Is(plan[i]))
                return i;
        }

        return -1;
    }

    internal void Fail(ErrorInfo error)
    {
        Error = error;
        IsUpdated = false;
        InterruptRunningSteps();
    }

    internal void FailRollback(ErrorInfo error)
    {
        Fail(error);
        RollbackFailed = true;
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
