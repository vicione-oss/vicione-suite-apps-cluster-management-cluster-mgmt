using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Public.Commands;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

public sealed class GeneralControlPanelSaveHandler(IUiMediator mediator, ISettingsService settingsService) :
    IControlPanelSaveHandler<GeneralControlPanelState>,
    IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async Task<ISaveResult> Save(GeneralControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.HasGridModeChanged)
            settingsService.SetGridMode(state.Settings.GetGridMode());

        if (state.HasMinimapNodeColoringChanged)
            settingsService.SetMinimapNodeColoring(state.Settings.MinimapNodeColoring);

        if (state.HasNodeAlignmentBorderChanged)
            settingsService.SetNodeAlignmentBorder(state.Settings.NodeAlignmentBorder);

        if (state.HasPanBehaviorChanged)
            settingsService.SetPanBehavior(state.Settings.PanBehavior);

        if (state.HasSimplifiedViewChanged)
            settingsService.SetSimplifiedView(state.Settings.SimplifiedView);

        var command = new AddOrUpdateClusterEditorSettings(state.Settings);
        await mediator.Send(command, cancellationToken);

        state.UpdateOriginalSettings();

        return new SaveSuccessResult(string.Empty);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
