using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

public sealed class IoddDetailsControlPanelDescriptor : IControlPanelDescriptor<IoddDetailsControlPanel>
{
    public string Title => Localization.Common.IoddDetails;
    public string IconPath => ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("device-outline.svg");
    public bool ShowInNavigation => false;
}
