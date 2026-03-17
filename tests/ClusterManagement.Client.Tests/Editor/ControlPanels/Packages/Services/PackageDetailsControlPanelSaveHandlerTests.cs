using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Services;

public sealed class PackageDetailsControlPanelSaveHandlerTests
{
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();

    private readonly ClusterDependency _dependency = new()
    {
        Name = "TestPackage",
        Version = new Version("1.4.0")
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _packageService)
            .AddSingleton(_ => _bannerService)
            .AddScoped<IControlPanelSaveHandler<PackageDetailsControlPanelState>, PackageDetailsControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_return_save_error_when_no_package_info_available()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);

        var state = new PackageDetailsControlPanelState();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackageDetailsControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        await _packageService.DidNotReceive().UpdatePackages(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_update_package_and_return_success()
    {
        // Arrange
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
        _packageService.UpdatePackages(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();

        var state = new PackageDetailsControlPanelState
        {
            PackageInfo = new ClusterPackageInfo() { Name = _dependency.Name },
            VersionToInstall = "1.5.0"
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackageDetailsControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _packageService.Received(1).UpdatePackages(
            Arg.Is<List<ClusterDependencyOperation>>(x => x.Any(k => k.Dependency.Name == _dependency.Name
            && k.Dependency.Version == new Version(state.VersionToInstall))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_error_when_update_fails()
    {
        // Arrange
        _packageService.UpdatePackages(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceErrorResult("update failed", 100));

        await using var serviceProvider = SetupServiceProvider();

        var state = new PackageDetailsControlPanelState
        {
            PackageInfo = new ClusterPackageInfo() { Name = _dependency.Name },
            VersionToInstall = "1.5.0"
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackageDetailsControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("update failed");
        errorResult.ErrorCode.Should().Be(100);
    }
}
