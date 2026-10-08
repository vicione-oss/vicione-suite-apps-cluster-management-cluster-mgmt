using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class ActiveClusterHeader
{
    private readonly string _editIconCssClass = MonochromeIconName.Edit.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _deployIconCssClass = MonochromeIconName.SendLight.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _sagaStateIconCssClass = MonochromeIconName.InfoLight.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _resetIconCssClass = MonochromeIconName.Reload.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public ClusterInfoModel Active { get; set; } = default!;
    [Parameter] public ClusterInfoModel? Draft { get; set; }
    [Parameter] public DeploymentProgressModel? Deployment { get; set; }
    [Parameter] public EventCallback OnOpenEditor { get; set; }
    [Parameter] public EventCallback OnDeployDraft { get; set; }
    [Parameter] public EventCallback OnShowSagaState { get; set; }
    [Parameter] public EventCallback OnResetSagaState { get; set; }

    private string DeployText => Draft is null
        ? ClusterOverview.Deploy
        : string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployVersion, Draft.Version);

    private string StatusName => Deployment switch
    {
        { IsRunning: true } => "deploying",
        { Phase: DeploymentPhase.Failed } => "failed",
        _ => "running",
    };

    private string StatusText => Deployment switch
    {
        { IsRunning: true } => string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployingVersion, Deployment.TargetVersion),
        { Phase: DeploymentPhase.Failed } => ClusterOverview.LastDeploymentFailed,
        _ => string.Format(CultureInfo.CurrentCulture, ClusterOverview.RunningVersion, Active.Version),
    };
}
