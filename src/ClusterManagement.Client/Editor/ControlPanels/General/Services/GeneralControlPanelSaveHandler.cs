using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.General.Services;

public sealed class GeneralControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<GeneralControlPanelState>,
    IEventConsumer<ClusterEditorSettingsChanged>
{
    private readonly ISettingsService _settingsService;

    public GeneralControlPanelSaveHandler(IUiMediator uiMediator, ISettingsService settingsService) : base(uiMediator)
    {
        _settingsService = settingsService;

        Register<ClusterEditorSettingsChanged>();
    }

    public override async Task<ISaveResult> Save(GeneralControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.HasGridModeChanged)
            _settingsService.SetGridMode(state.Settings.GetGridMode());

        if (state.HasMinimapNodeColoringChanged)
            _settingsService.SetMinimapNodeColoring(state.Settings.MinimapNodeColoring);

        if (state.HasNodeAlignmentBorderChanged)
            _settingsService.SetNodeAlignmentBorder(state.Settings.NodeAlignmentBorder);

        if (state.HasPanBehaviorChanged)
            _settingsService.SetPanBehavior(state.Settings.PanBehavior);

        if (state.HasSimplifiedViewChanged)
            _settingsService.SetSimplifiedView(state.Settings.SimplifiedView);

        var command = new AddOrUpdateClusterEditorSettings(state.Settings);
        var result = await SendAndWaitForCompletion(command, cancellationToken);
        if (result is SaveErrorResult errorResult)
            return errorResult;

        return new SaveSuccessResult();
    }

    public Task Consume(ClientContext<ClusterEditorSettingsChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
        }
        else
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }

        return Task.CompletedTask;
    }
}
