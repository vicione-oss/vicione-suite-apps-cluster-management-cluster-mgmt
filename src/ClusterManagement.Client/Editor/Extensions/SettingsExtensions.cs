using ClusterManagement.Internal.Contracts;
using ViciOne.Ui.ClusterEditor.Models;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class SettingsExtensions
{
    internal static EditorSettings Clone(this EditorSettings settings)
        => new()
        {
            GridMode = settings.GridMode,
            MinimapNodeColoring = settings.MinimapNodeColoring,
            NodeAlignmentBorder = settings.NodeAlignmentBorder,
            PanBehavior = settings.PanBehavior,
            SimplifiedView = settings.SimplifiedView,
            UserId = settings.UserId,
        };

    internal static GridMode GetGridMode(this EditorSettings settings)
        => (GridMode)settings.GridMode;

    internal static bool AreChanged(this EditorSettings settings, EditorSettings other)
        => settings.GridMode != other.GridMode ||
           settings.MinimapNodeColoring != other.MinimapNodeColoring ||
           settings.NodeAlignmentBorder != other.NodeAlignmentBorder ||
           settings.PanBehavior != other.PanBehavior ||
           settings.SimplifiedView != other.SimplifiedView;
}
