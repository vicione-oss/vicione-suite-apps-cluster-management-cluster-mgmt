using Bunit;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;
using Sdk.Testing.Client;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages;

public class PackagesControlPanelResetTests
{
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupDevExpressBlazor();
        ctx.SetupControlPanelServices();

        ctx.Services.AddSingleton(_packageService);
        ctx.Services.AddSingleton(_bannerService);
        ctx.Services.AddPackagesControlPanel();
        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new PackagesControlPanelState();

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }
}
