using ViciOne.Ui.ClusterEditor.Models;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class GridModeExtensions
{
    internal static string ToLocalizedString(this GridMode gridMode)
    {
        var gridModeNameKey = $"GridMode{gridMode}";

        return Localization.ClusterEditorSettings.ResourceManager.GetString(gridModeNameKey,
           Localization.ClusterEditorSettings.Culture) ?? gridMode.ToString();
    }
}
