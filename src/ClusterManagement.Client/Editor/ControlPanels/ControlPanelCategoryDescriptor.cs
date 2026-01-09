using ClusterManagement.Client.Editor.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.Editor.ControlPanels;

public sealed class ControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => ClusterEditor.ModuleTitle;
    public string? IconCssClass => null;
    public Uri? IconUrl => new(ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("cluster-editor.svg"), UriKind.Relative);
    public int? Position => 0;
}
