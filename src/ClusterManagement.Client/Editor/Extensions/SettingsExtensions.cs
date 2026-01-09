using ClusterManagement.Public.ClusterEditor;
using ViciOne.Ui.ClusterEditor.Models;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class SettingsExtensions
{
    internal static Settings Clone(this Settings settings)
        => new()
        {
            GridMode = settings.GridMode,
            MinimapNodeColoring = settings.MinimapNodeColoring,
            NodeAlignmentBorder = settings.NodeAlignmentBorder,
            PanBehavior = settings.PanBehavior,
            SimplifiedView = settings.SimplifiedView,
            UserId = settings.UserId,
        };

    internal static GridMode GetGridMode(this Settings settings)
        => (GridMode)settings.GridMode;

    internal static bool AreChanged(this Settings settings, Settings other)
        => settings.GridMode != other.GridMode ||
           settings.MinimapNodeColoring != other.MinimapNodeColoring ||
           settings.NodeAlignmentBorder != other.NodeAlignmentBorder ||
           settings.PanBehavior != other.PanBehavior ||
           settings.SimplifiedView != other.SimplifiedView;
}
