using ClusterManagement.Client.IoddManagement.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

public sealed class IoddControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => Common.DeviceDescription;
    public string? IconCssClass => null;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>("device-outline.svg");
    public int? Position => 5;
}
