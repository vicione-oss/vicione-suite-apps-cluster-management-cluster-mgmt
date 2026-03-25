using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Services;

public sealed class PackagesControlPanelSaveHandlerTests
{
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();

    private readonly ClusterDependency _dependency = new()
    {
        Name = "TestPackage",
        Version = "1.4.0"
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _packageService)
            .AddScoped<IControlPanelSaveHandler<PackagesControlPanelState>, PackagesControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_reset_pending_operations_success_when_there_are_no_pending_changes()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new PackagesControlPanelState();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackagesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_update_package_and_return_success()
    {
        // Arrange
        _packageService.UpdatePackages(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
        var state = new PackagesControlPanelState();
        state.InstallOperations.Add(operation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackagesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.InstallOperations.Should().BeEmpty();
        await _packageService.Received(1).UpdatePackages(
            Arg.Is<List<ClusterDependencyOperation>>(x => x.Any(k => k.Dependency.Name == _dependency.Name)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_error_when_update_fails()
    {
        // Arrange
        _packageService.UpdatePackages(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceErrorResult("update failed", 100));

        await using var serviceProvider = SetupServiceProvider();
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
        var state = new PackagesControlPanelState();
        state.InstallOperations.Add(operation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackagesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("update failed");
        errorResult.ErrorCode.Should().Be(100);
    }

    [Fact]
    public async Task Should_delete_packages_and_return_success()
    {
        // Arrange        
        await using var serviceProvider = SetupServiceProvider();
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
        var state = new PackagesControlPanelState();
        state.PackagesToBeDeleted.Add(new ClusterPackageItem(_dependency.Name, _dependency.Version.ToString()));

        _packageService.DeletePackage(_dependency.Name, _dependency.Version.ToString(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceSuccessResult());

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackagesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.PackagesToBeDeleted.Should().BeEmpty();
        await _packageService.Received(1).DeletePackage(_dependency.Name, _dependency.Version.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_return_error_when_delete_packages_fails()
    {
        // Arrange
        _packageService.DeletePackage(_dependency.Name, _dependency.Version.ToString(), Arg.Any<CancellationToken>())
            .Returns(new PackageServiceErrorResult("update failed", 100));

        await using var serviceProvider = SetupServiceProvider();
        var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
        var state = new PackagesControlPanelState();
        state.PackagesToBeDeleted.Add(new ClusterPackageItem(_dependency.Name, _dependency.Version.ToString()));

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<PackagesControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("update failed");
        errorResult.ErrorCode.Should().Be(100);
    }
}
