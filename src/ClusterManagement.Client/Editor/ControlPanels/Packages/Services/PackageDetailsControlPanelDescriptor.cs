using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

public sealed class PackageDetailsControlPanelDescriptor : IControlPanelDescriptor<PackageDetailsControlPanel>
{
    public string Title => Editor.Localization.Common.PackageDetails;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>("device-outline.svg");
    public bool ShowInNavigation => false;
}

