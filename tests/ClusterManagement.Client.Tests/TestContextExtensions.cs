using Bunit;
using ClusterManagement.Client.Management.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Blazor.Components.Breadcrumb.Extensions;
using ViciOne.Ui.Blazor.Components.Resizing.Services;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;

namespace ClusterManagement.Client.Tests;

internal static class TestContextExtensions
{
    public static BunitContext SetupControlPanelServices(this BunitContext ctx)
    {
        ctx.Services.AddSingleton(Substitute.For<IControlPanelRequest>());
        ctx.Services.AddSingleton(Substitute.For<IControlPanelPageRegistry>());
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
        ctx.JSInterop.ConfigureQuickGridJSInterop();

        return ctx;
    }

    /// <summary>
    /// What the page bar of the Cluster Manager pages needs: its toolbar, its breadcrumb and the Expert view.
    /// </summary>
    /// <remarks>
    /// The breadcrumb measures its items through a resize observer, which needs a browser; a substitute stands in.
    /// </remarks>
    public static BunitContext AddClusterPageBar(this BunitContext ctx, ExpertViewState? expertView = null)
    {
        ctx.Services.AddToolbar();
        ctx.Services.AddBreadcrumb();
        ctx.Services.AddSingleton(Substitute.For<IResizeObserver>());
        ctx.Services.AddSingleton(expertView ?? new ExpertViewState());

        return ctx;
    }
}
