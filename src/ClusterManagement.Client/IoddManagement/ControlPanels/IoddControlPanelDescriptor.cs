using ClusterManagement.Client.IoddManagement.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

public sealed class IoddControlPanelDescriptor : IControlPanelDescriptor<IoddControlPanel>
{
    public string Title => Common.IoLink;
    public string IconPath => ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("device-outline.svg");
}
