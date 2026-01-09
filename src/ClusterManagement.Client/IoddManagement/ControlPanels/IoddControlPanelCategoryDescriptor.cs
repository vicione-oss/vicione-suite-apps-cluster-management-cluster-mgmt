using ClusterManagement.Client.IoddManagement.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

public sealed class IoddControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => Common.DeviceDescription;
    public string? IconCssClass => null;
    public Uri? IconUrl => new(ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("device-outline.svg"), UriKind.Relative);
    public int? Position => 5;
}
