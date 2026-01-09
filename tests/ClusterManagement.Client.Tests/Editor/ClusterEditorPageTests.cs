using Blazor.Diagrams.Core.Geometry;
using Bunit;
using Bunit.TestDoubles;
using ClusterManagement.Client.Editor;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Client;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;
using Xunit;

namespace ClusterManagement.Client.Tests.Editor;

public sealed class DataflowEditorPageTests
{
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();

    [Fact]
    public void PageGetsRendered()
    {
        // Arrange
        var version = new { Version = "a8b21501-update-ip-addresses" };

        using var ctx = new TestContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddLocalization<ClusterManagementClientModule>();
        });
        ctx.SetupDevExpressBlazor();
        ctx.Services.AddSingleton(_clusterService);

        ctx.Services.AddScoped(_ => Substitute.For<IDesignProvider>());
        ctx.Services.AddScoped<LoadClusterService>();
        ctx.Services.AddScoped<ExportClusterService>();
        ctx.Services.AddScoped<SaveClusterService>();
        ctx.Services.AddScoped<LoadFunctionBlocksService>();
        ctx.Services.AddScoped<ClusterWizardDialogService>();
        ctx.Services.AddTransient(_ => HttpClientFactory.GetHttpClientWithResponse(version));

        ctx.Services.AddClusterEditor(s => Substitute.For<IRulesetProvider>());

        ctx.JSInterop.SetupVoid("ViciOne.Observer.observe", _ => true);
        ctx.JSInterop.Setup<Rectangle>("ZBlazorDiagrams.getBoundingClientRect", _ => true);

        var authContext = ctx.AddTestAuthorization();
        authContext.SetAuthorizing();

        // Act + Assert
        var sut = ctx.RenderComponent<ClusterEditorPage>();
        Assert.NotNull(sut);
    }
}
