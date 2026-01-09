using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.Services;

public sealed class GeneralControlPanelResetHandler() :
    IControlPanelResetHandler<GeneralControlPanelState>
{
    public Task Reset(GeneralControlPanelState state, CancellationToken cancellationToken)
    {
        state.Reset();
        return Task.CompletedTask;
    }
}
