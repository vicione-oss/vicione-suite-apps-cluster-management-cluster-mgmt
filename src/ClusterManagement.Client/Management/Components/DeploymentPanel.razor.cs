using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management.Components;

/// <summary>
/// Always shown, so the page does not move when a deployment starts: without a deployment in this session it shows
/// the running version as the result of the last one.
/// </summary>
public sealed partial class DeploymentPanel : IDisposable
{
    private static readonly TimeSpan s_stepClockInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<Guid> _expanded = [];
    private readonly string _sagaStateIconCssClass = MonochromeIconName.InfoLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _resetIconCssClass = MonochromeIconName.Reload.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();

    private bool _showAll;
    private ITimer? _stepClock;
    private DeploymentProgressModel? _expandedIn;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    [Parameter] public DeploymentProgressModel? Deployment { get; set; }

    /// <summary>
    /// <c>null</c> while the first version of the cluster is being deployed.
    /// </summary>
    [Parameter] public ClusterInfoModel? Running { get; set; }

    /// <summary>
    /// Adds the steps of each instance, the instances listed on request and the deployment state machine.
    /// </summary>
    [Parameter] public bool Expert { get; set; }

    /// <summary>
    /// The user holds the Engine control feature, which resetting the deployment state needs as well.
    /// </summary>
    [Parameter] public bool MayReset { get; set; }

    [Parameter] public EventCallback OnShowSagaState { get; set; }
    [Parameter] public EventCallback OnResetSagaState { get; set; }

    /// <summary>
    /// The failed instances and, while the deployment runs, those that reported a step, or every instance once the user
    /// asked for all of them.
    /// </summary>
    internal IEnumerable<InstanceProgressModel> VisibleInstances
    {
        get
        {
            if (Deployment is null)
                return [];

            return Expert && _showAll ? Deployment.Instances : Deployment.Instances.Where(IsListedByDefault);
        }
    }

    // The toggle is offered only while it changes the list.
    private bool HasInstancesListedOnRequest => Deployment?.Instances.Any(i => !IsListedByDefault(i)) == true;

    public void Dispose()
        => StopStepClock();

    // The elapsed time of the steps in progress moves on by itself, so the panel renders every second while it can change.
    protected override void OnParametersSet()
    {
        // The steps of every instance start collapsed, also in the next deployment.
        if (!ReferenceEquals(Deployment, _expandedIn))
        {
            _expanded.Clear();
            _expandedIn = Deployment;
        }

        if (Deployment?.IsRunning == true)
            _stepClock ??= TimeProvider.CreateTimer(_ => _ = InvokeAsync(TickStepClock), null, s_stepClockInterval, s_stepClockInterval);
        else
            StopStepClock();
    }

    private void TickStepClock()
    {
        if (_stepClock is null)
            return;

        if (Deployment?.IsRunning == true)
            StateHasChanged();
        else
            StopStepClock();
    }

    private void StopStepClock()
    {
        _stepClock?.Dispose();
        _stepClock = null;
    }

    // The only instance is listed from the start, the others from their first step on. A listed instance stays listed
    // once the deployment ended: on a standalone its steps arrive together just before the end and would show for a moment.
    private bool IsListedByDefault(InstanceProgressModel instance)
        => instance.HasFailed || instance.Steps.Count > 0 || Deployment?.Instances.Count == 1;

    // A deployment that ended shows no step in progress, even when the event that ended the step was lost.
    private InstanceStepModel? StepInProgress(InstanceProgressModel instance)
        => Deployment?.IsRunning == true ? instance.CurrentStep : null;

    private void ToggleSteps(Guid instanceId)
    {
        if (!_expanded.Remove(instanceId))
            _expanded.Add(instanceId);
    }

    // The step in progress, or how long the update or the rollback of an instance took; nothing between two steps.
    private StepSummary? Summary(InstanceProgressModel instance)
    {
        if (StepInProgress(instance) is { } step)
            return new(step.ToText(), Elapsed(step), "current");

        if (FinishedRunText(instance) is { } text && instance.Duration is { } duration)
            return new(text, duration.ToDisplayText(CultureInfo.CurrentCulture), "done");

        return null;
    }

    // The saga reports no end of an instance's rollback, so the rollback counts as finished once the deployment ended.
    private string? FinishedRunText(InstanceProgressModel instance)
    {
        var steps = instance.LatestRun.Count;
        if (instance.RollbackStarted)
            return Deployment?.IsRunning == true ? null : CountText(steps, ClusterOverview.StepsRolledBackOne, ClusterOverview.StepsRolledBack);

        return instance.IsUpdated ? CountText(steps, ClusterOverview.StepsUpdatedOne, ClusterOverview.StepsUpdated) : null;
    }

    private static string CountText(int count, string one, string many)
        => count == 1 ? one : Format(many, count);

    private string StepTime(InstanceStepModel step)
        => StateOf(step) == InstanceStepState.Running
            ? Elapsed(step)
            : step.Duration?.ToDisplayText(CultureInfo.CurrentCulture) ?? "–";

    private string Elapsed(InstanceStepModel step)
        => step.StartedAt is { } startedAt ? (TimeProvider.GetUtcNow() - startedAt).ToStopwatchText() : string.Empty;

    // A step whose end never came is no longer running once the deployment ended.
    private InstanceStepState StateOf(InstanceStepModel step)
        => step.State == InstanceStepState.Running && Deployment?.IsRunning != true ? InstanceStepState.Interrupted : step.State;

    private string StateClass(InstanceStepModel step) => StateOf(step) switch
    {
        InstanceStepState.Succeeded => "done",
        InstanceStepState.Running => "current",
        InstanceStepState.Failed => "failed",
        InstanceStepState.Refused => "refused",
        _ => "interrupted",
    };

    private string StateMark(InstanceStepModel step) => StateOf(step) switch
    {
        InstanceStepState.Succeeded => "✓",
        InstanceStepState.Running => "●",
        InstanceStepState.Failed => "✕",
        InstanceStepState.Refused => "!",
        _ => "–",
    };

    private string StateText(InstanceStepModel step) => StateOf(step) switch
    {
        InstanceStepState.Succeeded => ClusterOverview.StepStateSucceeded,
        InstanceStepState.Running => ClusterOverview.StepStateRunning,
        InstanceStepState.Failed => ClusterOverview.StepStateFailed,
        InstanceStepState.Refused => ClusterOverview.StepStateRefused,
        _ => ClusterOverview.StepStateInterrupted,
    };

    // A node named after its instance, as on a standalone ("IFM10N34571 (Physical)"), is not repeated.
    private static string? NodeText(InstanceProgressModel instance)
        => instance.NodeName == instance.Name || instance.NodeName.StartsWith($"{instance.Name} (", StringComparison.Ordinal)
            ? null
            : Format(ClusterOverview.OnNode, instance.NodeName);

    private static string? ErrorText(InstanceProgressModel instance, ErrorInfo error)
        => instance.FailedStep is { } step ? Format(ClusterOverview.StepFailed, step.ToText(), error.Message) : error.Message;

    private string ShowInstancesText => (_showAll, Deployment?.Instances.Count) switch
    {
        (true, _) => ClusterOverview.ShowFewer,
        (false, 1) => ClusterOverview.ShowInstance,
        (false, var count) => Format(ClusterOverview.ShowAllInstances, count),
    };

    private IReadOnlyList<DeploymentStep> Steps => Deployment?.GetSteps() ?? DeploymentProgressModel.CompletedSteps;

    private string StartedText => Deployment?.StartedAt is { } startedAt
        ? Format(ClusterOverview.StartedAt, startedAt.To24HourTime(TimeProvider.LocalTimeZone))
        : string.Empty;

    private string Headline => Deployment?.Phase switch
    {
        null => Running is null ? ClusterOverview.NoActiveVersion : Format(ClusterOverview.RunningVersionDeployed, Running.Version),
        DeploymentPhase.Completed => Format(ClusterOverview.DeploymentCompleted, Deployment.TargetVersion, Deployment.Duration?.ToDisplayText(CultureInfo.CurrentCulture)),
        DeploymentPhase.Failed => Format(ClusterOverview.DeploymentFailed, Deployment.TargetVersion),
        _ => Format(ClusterOverview.DeployingVersion, Deployment.TargetVersion),
    };

    private string StatusLine
    {
        get
        {
            if (Deployment is null)
            {
                return Running?.Deployed is { } deployed
                    ? Format(ClusterOverview.NoDeploymentObserved, deployed.ToShortDateAnd24HourTime(TimeProvider.LocalTimeZone))
                    : string.Empty;
            }

            if (Deployment.Phase == DeploymentPhase.Failed)
                return FailureText(Deployment);

            return Format(ClusterOverview.InstancesUpdated, Deployment.Instances.Count(i => i.IsUpdated), Deployment.Instances.Count);
        }
    }

    private static string FailureText(DeploymentProgressModel deployment)
    {
        var duration = deployment.Duration?.ToDisplayText(CultureInfo.CurrentCulture);
        if (deployment.WasReset)
            return Format(ClusterOverview.ResetInPhase, deployment.FailedPhase?.ToText(), duration);

        var failedIn = Format(ClusterOverview.FailedInPhase,
            (deployment.FailurePhase ?? deployment.ForwardPhase).ToText(),
            duration);

        // Before the first phase no step is marked failed and no instance has an error, so only the reason explains it.
        if (deployment.ForwardPhase == DeploymentPhase.Starting && deployment.FailureReason is { } reason)
            return $"{failedIn}. {reason}";

        if (deployment.RollbackFailed)
            return $"{failedIn}. {ClusterOverview.RollbackFailed}";

        return deployment.HasRolledBack ? $"{failedIn}. {ClusterOverview.RolledBack}" : failedIn;
    }

    private static string Format(string format, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);

    private static string StepClass(DeploymentStepState state) => state switch
    {
        DeploymentStepState.Done => "done",
        DeploymentStepState.Failed => "failed",
        DeploymentStepState.Current => "current",
        _ => "pending",
    };

    private static string StepMark(DeploymentStepState state) => state switch
    {
        DeploymentStepState.Done => "✓",
        DeploymentStepState.Failed => "✕",
        DeploymentStepState.Current => "●",
        _ => "○",
    };

    private static string InstanceStatusName(InstanceProgressModel instance) => instance switch
    {
        { HasFailed: true } => "failed",
        { RollbackStarted: true } => "warning",
        { IsUpdated: true } => "running",
        _ => "deploying",
    };

    private string InstanceStatusText(InstanceProgressModel instance) => instance switch
    {
        { HasFailed: true } => ClusterOverview.InstanceFailed,
        { RollbackStarted: true } => Deployment?.IsRunning == true ? ClusterOverview.InstanceRollingBack : ClusterOverview.InstanceRolledBack,
        { IsUpdated: true } => ClusterOverview.InstanceUpdated,
        { IsStarted: true } or { Steps.Count: > 0 } => ClusterOverview.InstanceStarted,
        _ => ClusterOverview.InstanceWaiting,
    };

    private sealed record StepSummary(string Text, string Time, string CssClass);
}
