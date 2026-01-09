using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Extensions;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.DataflowEvents
{
    public sealed partial class DataflowEventsPage
    {
        [Inject]
        private DataflowEventPageService PageService { get; set; } = default!;

        [Inject] private NavigationManager NavManager { get; set; } = default!;


        protected override async Task OnInitializedAsync()
        {
            await PageService.LoadDataflowEvents();
            PageService.RefreshRequested += async () => await InvokeAsync(StateHasChanged);

            await base.OnInitializedAsync();
        }

        private async Task RefreshDataflowEvents()
        {
            await PageService.LoadDataflowEvents();

            await InvokeAsync(StateHasChanged);
        }

        private void SelectedEventChanged(object obj)
            => PageService.SelectedEvent = obj as DataflowEventModel;

        private void NavigateToManagement()
            => NavManager.NavigateToManagement();

        protected override async ValueTask DisposeInternal()
        {
            if (PageService != null)
                await PageService.DisposeAsync();

            await base.DisposeInternal();
        }
    }
}
