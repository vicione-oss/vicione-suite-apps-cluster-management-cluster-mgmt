using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

public sealed class IoddDetailsControlPanelDescriptor : IControlPanelDescriptor<IoddDetailsControlPanel>
{
    public string Title => Localization.Common.IoddDetails;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>("device-outline.svg");
    public bool ShowInNavigation => false;
}
