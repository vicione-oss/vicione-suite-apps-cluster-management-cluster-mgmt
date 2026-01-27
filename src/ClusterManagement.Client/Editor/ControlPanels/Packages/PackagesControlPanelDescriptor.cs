using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

public sealed class PackagesControlPanelDescriptor : IControlPanelDescriptor<PackagesControlPanel>
{
    public string Title => Localization.Common.Title;
    public string IconPath => ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("device-outline.svg");
}
