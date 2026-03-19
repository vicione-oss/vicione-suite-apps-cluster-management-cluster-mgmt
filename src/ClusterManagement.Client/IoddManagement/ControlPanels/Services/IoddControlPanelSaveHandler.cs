using ClusterManagement.Client.IoddManagement.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.IoddManagement.ControlPanels.Services;

internal sealed class IoddControlPanelSaveHandler(IIoddService ioddService)
        : IControlPanelSaveHandler<IoddControlPanelState>
{
    public async Task<ISaveResult> Save(IoddControlPanelState state, CancellationToken cancellationToken)
    {
        var toBeDeleted = state.DevicesMarkedForDeletion.ToArray();
        foreach (var id in toBeDeleted)
        {
            var deleteResult = await ioddService.DeleteIodd(id.VendorId, id.DeviceId, cancellationToken);
            if (deleteResult is IoddServiceErrorResult deleteError)
                return new SaveErrorResult(deleteError.ErrorMessage, deleteError.ErrorCode);

            // On success we remove it from the list because it's deleted already
            state.DevicesMarkedForDeletion.Remove(id);
        }

        return new SaveSuccessResult();
    }
}
