using Bunit;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;
using Sdk.Testing.Client;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages;

public class PackageDetailsControlPanelTests
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
        ctx.Services.AddPackageControlPanel();
        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new PackageDetailsControlPanelState
        {
            PackageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                Description = "A test package"
            }
        };

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<PackageDetailsControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }
}
