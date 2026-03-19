using ClusterManagement.Client.IoddManagement.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

public sealed class IoddControlPanelDescriptor : IControlPanelDescriptor<IoddControlPanel>
{
    public string Title => Common.IoLink;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>("device-outline.svg");
}
