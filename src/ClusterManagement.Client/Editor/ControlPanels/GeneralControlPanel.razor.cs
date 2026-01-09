using ClusterManagement.Client.Editor.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.ClusterEditor.Models;

namespace ClusterManagement.Client.Editor.ControlPanels;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public sealed partial class GeneralControlPanel
{
    private readonly List<ComboBoxItem<int, string>> _gridModeComboBoxItems = [..
        Enum.GetValues<GridMode>().Select(gm => new ComboBoxItem<int, string>
        {
            Value = (int)gm,
            Text = gm.ToLocalizedString()
        })];

    [CascadingParameter]
    public required Task<AuthenticationState> AuthState { get; set; }

    private async Task AfterChange()
    {
        if (State.HasChanges())
            await BeginEdit();
        else
            await CancelEdit();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        var username = (await AuthState).User.Identity?.Name;

        await State.Initialize(username);
    }
}
