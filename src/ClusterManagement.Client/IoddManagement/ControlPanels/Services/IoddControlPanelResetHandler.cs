using ClusterManagement.Client.IoddManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

internal sealed class IoddControlPanelResetHandler(IIoddService ioddService) : IControlPanelResetHandler<IoddControlPanelState>
{
    public async Task Reset(IoddControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();

        try
        {
            state.Devices.Clear();
            state.DevicesMarkedForDeletion.Clear();

            state.Devices = [.. await ioddService.GetIODevices(cancellationToken)];
            state.AutoDownload = await ioddService.GetIsAutoDownloading(cancellationToken);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
