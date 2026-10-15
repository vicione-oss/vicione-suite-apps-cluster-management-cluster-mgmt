using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.DebugPages;

/// <summary>
/// The actions of one engine on the Engines page: its packages for everyone, and starting, stopping and tearing it
/// down for those who may control engines.
/// </summary>
public sealed partial class EngineActions
{
    private static readonly string s_startIconCssClass = IconCssClass(MonochromeIconName.SendSolid);
    private static readonly string s_stopIconCssClass = IconCssClass(MonochromeIconName.SwitchOff);
    private static readonly string s_tearDownIconCssClass = IconCssClass(MonochromeIconName.Delete);
    private static readonly string s_packagesIconCssClass = IconCssClass(MonochromeIconName.InfoLight);

    [Parameter, EditorRequired] public EngineRuntimeModel Engine { get; set; } = default!;

    [Parameter] public bool MayControl { get; set; }

    /// <summary>
    /// While a deployment runs, the state shown is the one from before it, so no action relies on it.
    /// </summary>
    [Parameter] public bool Deploying { get; set; }

    [Parameter] public EventCallback<EngineRuntimeModel> OnStart { get; set; }
    [Parameter] public EventCallback<EngineRuntimeModel> OnStop { get; set; }
    [Parameter] public EventCallback<EngineRuntimeModel> OnTearDown { get; set; }
    [Parameter] public EventCallback<EngineRuntimeModel> OnShowPackages { get; set; }

    private bool CanStart => !Deploying && Engine.IsStopped;

    private bool CanStop => !Deploying && Engine is { IsReachable: true, State: EngineState.Running };

    private bool CanTearDown => !Deploying && Engine.IsStopped;

    private (EngineRuntimeModel Engine, bool MayControl, bool Deploying)? _shown;
    private bool _hasChanged;

    // The page renders every second; a row whose engine, right and deployment did not change keeps its buttons.
    protected override void OnParametersSet()
    {
        var shown = (Engine, MayControl, Deploying);
        _hasChanged = _shown != shown;
        _shown = shown;
    }

    protected override bool ShouldRender()
        => _hasChanged;

    private static string IconCssClass(MonochromeIconName icon)
        => icon.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
}
