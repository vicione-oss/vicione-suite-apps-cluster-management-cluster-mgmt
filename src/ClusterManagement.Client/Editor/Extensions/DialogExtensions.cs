using ViciOne.Ui.Blazor.Components.Dialog.Components;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class DialogExtensions
{
    public static async Task OpenOrCloseDialog(this Dialog? dialog, bool visible)
    {
        if (dialog is null)
            return;

        if (visible)
            await dialog.ShowAsync();
        else
            await dialog.CloseAsync();
    }
}
