using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Editor.Services;

public sealed class GeneralControlPanelState(IUiMediator mediator) :
    ControlPanelState,
    IDisposable
{
    private const string DefaultName = "Anonymous";

    private readonly CancellationTokenSource _cancellationTokenSource = new();

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

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    public async Task Initialize(string? username)
    {
        BeginLoading();

        try
        {
            var userId = username ?? DefaultName;
            var request = new GetClusterEditorSettingsRequest(userId);
            var response = await mediator.Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(request);

            _currentSettings = response.RequestError is not null ? new EditorSettings() { UserId = userId } : response.Settings!;
            UpdateOriginalSettings();
        }
        finally
        {
            EndLoading();
        }
    }

    public bool HasChanges()
        => _currentSettings.AreChanged(_originalSettings);

    public void Reset()
        => _currentSettings = _originalSettings.Clone();

    public void UpdateOriginalSettings()
        => _originalSettings = _currentSettings.Clone();
}
