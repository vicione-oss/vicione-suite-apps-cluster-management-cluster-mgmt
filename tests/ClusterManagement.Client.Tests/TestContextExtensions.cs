using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;

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
}
