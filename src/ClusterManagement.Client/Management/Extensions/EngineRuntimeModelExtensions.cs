using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;

namespace ClusterManagement.Client.Management.Extensions;

/// <summary>
/// How an engine's state reads on the Cluster Manager pages: the status class of its dot and its text.
/// </summary>
internal static class EngineRuntimeModelExtensions
{
    /// <summary>
    /// While a deployment runs, the engine shows that instead of the state it had before; before its first answer, it
    /// shows nothing yet.
    /// </summary>
    public static string StatusName(this EngineRuntimeModel engine, bool deploying)
        => deploying ? "deploying" : engine.IsChecked ? engine.StatusName() : "previous";

    /// <inheritdoc cref="StatusName(EngineRuntimeModel, bool)"/>
    public static string StateText(this EngineRuntimeModel engine, bool deploying)
        => deploying ? ClusterOverview.EngineDeploying : engine.IsChecked ? engine.StateText() : "…";

    /// <summary>
    /// How many of the engines run, for an engine host or the whole cluster. While a deployment runs it says that
    /// instead; before every engine has answered it is <c>null</c>, so no count of 0 shows for a moment.
    /// </summary>
    public static string? RunningText(this IReadOnlyList<EngineRuntimeModel> engines, bool deploying)
        => deploying ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.EnginesDeploying, engines.Count, engines.Count)
            : engines.All(engine => engine.IsChecked)
                ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.EnginesRunning, engines.Count(engine => engine.IsRunning), engines.Count)
                : null;

    public static string StatusName(this EngineRuntimeModel engine) => engine switch
    {
        { IsDeployed: false } => "previous",
        { IsReachable: false } => "failed",
        { State: EngineState.StartFailed } => "failed",
        { State: EngineState.Running } => "running",
        { State: EngineState.Starting or EngineState.Stopping } => "deploying",
        _ => "previous",
    };

    public static string StateText(this EngineRuntimeModel engine) => engine switch
    {
        { IsDeployed: false } => ClusterOverview.EngineNotDeployed,
        { IsReachable: false } => ClusterOverview.EngineNotReachable,
        { State: EngineState.StartFailed } => ClusterOverview.EngineStartFailed,
        { State: EngineState.Running } => ClusterOverview.EngineRunning,
        { State: EngineState.Starting } => ClusterOverview.EngineStarting,
        { State: EngineState.Stopping } => ClusterOverview.EngineStopping,
        _ => ClusterOverview.EngineStopped,
    };
}
