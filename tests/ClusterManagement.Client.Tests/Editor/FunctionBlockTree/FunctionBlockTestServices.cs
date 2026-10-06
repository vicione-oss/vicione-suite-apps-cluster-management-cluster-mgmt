using Bunit;
using ClusterManagement.Client.Editor.FunctionBlockTree;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace ClusterManagement.Client.Tests.Editor.FunctionBlockTree;

internal static class FunctionBlockTestServices
{
    public static BunitContext SetupFunctionBlockSelection(this BunitContext ctx)
    {
        ctx.SetupSuiteServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddCheckBox();
        ctx.Services.AddSingleton(Substitute.For<IMonochromeIconSvgMarkupProvider>());
        ctx.Services.AddTransient<FunctionBlockTreeAdapter>();

        return ctx;
    }
}
