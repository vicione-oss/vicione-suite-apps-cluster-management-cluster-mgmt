using ViciOne.Ui.Shared.Dx.Components;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class DxDialogExtensions
{
    public static async Task OpenOrCloseDialog(this DxDialog? dialog, bool visible)
    {
        if (dialog is null)
            return;

        if (visible)
            await dialog.OpenAsync();
        else
            await dialog.CloseAsync();
    }
}
