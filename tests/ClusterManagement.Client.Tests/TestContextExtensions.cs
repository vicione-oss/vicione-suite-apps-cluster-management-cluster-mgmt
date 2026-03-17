using Bunit;
using DevExpress.Blazor.Internal;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Tests;

internal static class TestContextExtensions
{
    public static BunitContext SetupDevExpressBlazor(this BunitContext ctx)
    {
        var env = Substitute.For<IEnvironmentInfo>();
        env.DeviceInfo.Returns(new DeviceInfo(false));

        ctx.Services.AddScoped(s => env);
        ctx.Services.AddDevExpressBlazor(options => options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var rootModule = ctx.JSInterop.SetupModule("./_content/DexExpress.Blazor/dx-blazor.js");
        rootModule.Mode = JSRuntimeMode.Strict;
        rootModule.Setup<DeviceInfo>("getDeviceInfo", _ => true).SetResult(new DeviceInfo(false));

        return ctx;
    }

    public static BunitContext SetupControlPanelServices(this BunitContext ctx)
    {
        ctx.Services.AddSingleton(Substitute.For<IControlPanelRequest>());
        ctx.Services.AddSingleton(Substitute.For<IControlPanelPageRegistry>());
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
        ctx.JSInterop.ConfigureQuickGridJSInterop();

        return ctx;
    }
}
