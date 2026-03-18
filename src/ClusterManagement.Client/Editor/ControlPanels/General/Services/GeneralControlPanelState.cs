using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Internal.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.General.Services;

public sealed class GeneralControlPanelState() : ControlPanelState
{
    private const string DefaultName = "Anonymous";

    private EditorSettings _currentSettings = new() { UserId = DefaultName };
    private EditorSettings _originalSettings = new() { UserId = DefaultName };

    public EditorSettings Settings => _currentSettings;

    public bool HasGridModeChanged
        => _currentSettings.GridMode != _originalSettings.GridMode;

    public bool HasSimplifiedViewChanged
        => _currentSettings.SimplifiedView != _originalSettings.SimplifiedView;

    public bool HasMinimapNodeColoringChanged
        => _currentSettings.MinimapNodeColoring != _originalSettings.MinimapNodeColoring;

    public bool HasNodeAlignmentBorderChanged
        => _currentSettings.NodeAlignmentBorder != _originalSettings.NodeAlignmentBorder;

    public bool HasPanBehaviorChanged
        => _currentSettings.PanBehavior != _originalSettings.PanBehavior;

    public void Initialize(EditorSettings settings)
    {
        _currentSettings = settings;
        UpdateOriginalSettings();
    }

    public bool HasChanges()
        => _currentSettings.AreChanged(_originalSettings);

    private void UpdateOriginalSettings()
        => _originalSettings = _currentSettings.Clone();
}
