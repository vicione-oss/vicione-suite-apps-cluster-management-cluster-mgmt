using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Services;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Breadcrumb.Models;

namespace ClusterManagement.Client.Management.Components;

/// <summary>
/// The bar under the top bar on every Cluster Manager page: where the user is, what the running cluster is doing, the
/// Expert view and the actions of the page.
/// </summary>
/// <remarks>
/// The trail starts at the cluster and, on another page, names that page; the cluster is the way back.
/// </remarks>
public sealed partial class ClusterPageBar : IDisposable
{
    private BreadcrumbItem _current = new() { Name = Common.ModuleTitle };
    private (ClusterPage Page, string? ClusterName)? _trail;

    [Inject] private ExpertViewState ExpertView { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public ClusterPage Page { get; set; }

    /// <summary>
    /// <c>null</c> until the page has loaded the running cluster; the trail then starts at the Cluster Manager.
    /// </summary>
    [Parameter] public string? ClusterName { get; set; }

    [Parameter] public Guid? ClusterId { get; set; }
    [Parameter] public Version? RunningVersion { get; set; }
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>
    /// A newer version that is ready to deploy; only the overview, which can deploy it, passes it.
    /// </summary>
    [Parameter] public Version? DraftVersion { get; set; }

    /// <summary>
    /// The actions of the page, as toolbar items, after the Expert view.
    /// </summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    protected override void OnInitialized()
        => ExpertView.Changed += OnExpertViewChanged;

    // The breadcrumb keys its items by value, so a trail built anew for every render would replace its elements each time.
    protected override void OnParametersSet()
    {
        if (_trail == (Page, ClusterName))
            return;

        _trail = (Page, ClusterName);
        _current = BuildTrail();
    }

    public void Dispose()
        => ExpertView.Changed -= OnExpertViewChanged;

    private BreadcrumbItem BuildTrail()
    {
        var cluster = new BreadcrumbItem { Name = ClusterName ?? Common.ModuleTitle };
        return Page switch
        {
            ClusterPage.EventViewer => cluster.AddChild(new BreadcrumbItem { Name = ClusterOverview.EventViewer }),
            ClusterPage.Engines => cluster.AddChild(new BreadcrumbItem { Name = ClusterOverview.Engines }),
            _ => cluster,
        };
    }

    // Only the cluster can be clicked; the page the user is on is the current item.
    private void Navigate(BreadcrumbItem item)
    {
        if (item != _current)
            Navigation.NavigateToManagement();
    }

    private void OnExpertViewChanged()
        => _ = InvokeAsync(StateHasChanged);
}
