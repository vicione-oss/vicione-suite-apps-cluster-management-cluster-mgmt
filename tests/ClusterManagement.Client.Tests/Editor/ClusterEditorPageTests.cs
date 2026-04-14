using Blazor.Diagrams.Core.Geometry;
using Bunit;
using ClusterManagement.Client.Editor;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Client;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Tests.Editor;

public sealed class DataflowEditorPageTests
{
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();

    [Fact]
    public async Task PageGetsRendered()
    {
        // Arrange
        var version = new { Version = "a8b21501-update-ip-addresses" };

        await using var ctx = new BunitContext();
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
        ctx.Services.AddScoped(_ => Substitute.For<IResourceDownloadStateService>());
        ctx.Services.AddScoped<ClusterWizardDialogService>();
        ctx.Services.AddTransient(_ => HttpClientFactory.GetHttpClientWithResponse(version));

        ctx.Services.AddClusterEditor(s => Substitute.For<IRulesetProvider>());

        ctx.JSInterop.SetupVoid("ViciOne.Observer.observe", _ => true);
        ctx.JSInterop.Setup<Rectangle>("ZBlazorDiagrams.getBoundingClientRect", _ => true);

        var authContext = ctx.AddAuthorization();
        authContext.SetAuthorizing();

        // Act + Assert
        var sut = ctx.Render<ClusterEditorPage>();
        Assert.NotNull(sut);
    }
}
