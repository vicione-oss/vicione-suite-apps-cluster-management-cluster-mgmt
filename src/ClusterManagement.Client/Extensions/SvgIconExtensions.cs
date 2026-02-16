using ClusterManagement.Client.Editor.Enums;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;

namespace ClusterManagement.Client.Extensions;

internal static class SvgIconExtensions
{
    internal static string GetPath(this SvgIcon icon)
    {
        var iconPath = ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>($"{icon.ToString().ToHyphenSeparated()}.svg");

        return iconPath.OriginalString;
    }
}
