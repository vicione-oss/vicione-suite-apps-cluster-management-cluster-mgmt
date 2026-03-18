using ClusterManagement.Client.Editor.Extensions;
using Sdk.Client.ControlPanels.Attributes;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.ClusterEditor.Models;

namespace ClusterManagement.Client.Editor.ControlPanels.General;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public sealed partial class GeneralControlPanel
{
    private readonly List<ComboBoxItem<int, string>> _gridModeComboBoxItems = [..
        Enum.GetValues<GridMode>().Select(gm => new ComboBoxItem<int, string>
        {
            Value = (int)gm,
            Text = gm.ToLocalizedString()
        })];

    private async Task AfterChange()
    {
        if (State.HasChanges())
            await BeginEdit();
        else
            await CancelEdit();
    }
}
