using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.DataflowEvents;

public sealed partial class DataflowEventsPage
{
    private readonly string _plusIconCssClass = MonochromeIconName.PlusSlim.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _minusIconCssClass = MonochromeIconName.MinusSlim.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();

    [Inject]
    private DataflowEventPageService PageService { get; set; } = default!;

    [Inject] private NavigationManager NavManager { get; set; } = default!;

    protected override string PageTitle => Localization.DataflowEventsPage.PageTitle;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await PageService.LoadDataflowEvents();
        PageService.RefreshRequested += OnRefreshRequested;
    }

    private async Task RefreshDataflowEvents()
    {
        await PageService.LoadDataflowEvents();

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnRefreshRequested()
        => await InvokeAsync(StateHasChanged);

    private void SelectedEventChanged(object obj)
        => PageService.SelectedEvent = obj as DataflowEventModel;

    private void NavigateToManagement()
        => NavManager.NavigateToManagement();

    protected override async ValueTask DisposeInternal()
    {
        if (PageService != null)
        {
            PageService.RefreshRequested -= OnRefreshRequested;
            await PageService.DisposeAsync();
        }

        await base.DisposeInternal();
    }
}
