using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Services;

public sealed class PackagesControlPanelResetHandlerTests
{
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();

    private readonly ClusterDependency _dependency = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private readonly ClusterDependency _anotherDependency = new()
    {
        Name = "ViciOne.OtherPackage",
        Version = "2.1.17"
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_packageService)
            .AddScoped<IControlPanelResetHandler<PackagesControlPanelState>, PackagesControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_reset_pending_operations()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new PackagesControlPanelState();
        state.InstallOperations.Add(new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install));
        state.UninstallOperations.Add(new ClusterDependencyOperation(_anotherDependency, ClusterDependencyOperationKind.Uninstall));
        state.PackagesToBeDeleted.Add(new ClusterPackageItem("Some.Package", "12.13.1"));

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<PackagesControlPanelState>>();

        // Act
        await saveHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.InstallOperations.Should().BeEmpty();
        state.UninstallOperations.Should().BeEmpty();
        state.PackagesToBeDeleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_set_installed_and_available_queryables()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var packageInfo = new ClusterPackageInfo
        {
            Name = _dependency.Name,
            PendingOperation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install)
        };

        var state = new PackagesControlPanelState();

        _packageService.GetPackages(null, Arg.Any<CancellationToken>())
            .Returns(
            [
                new ClusterPackageInfo
                {
                    Name = _dependency.Name,
                    InstalledVersion = "1.0.0",
                },
                new ClusterPackageInfo
                {
                    Name = _anotherDependency.Name,
                    InstalledVersion = "2.0.0",
                    AvailableUpdateVersion = "2.1.17"
                },
                new ClusterPackageInfo
                {
                    Name = "Available",
                    Versions = [ new ClusterPackageVersion("1.23.4"),
                                 new ClusterPackageVersion("1.34.1") ],
                }
            ]);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<PackagesControlPanelState>>();

        // Act
        await saveHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.InstalledQueryable.Should().ContainSingle(k => k.Name == _dependency.Name);
        state.InstalledQueryable.Should().ContainSingle(k => k.Name == _anotherDependency.Name);
        state.AvailableQueryable.Should().ContainSingle(k => k.Name == "Available");
    }

    [Fact]
    public async Task Should_not_execute_if_we_have_packages_and_no_pending_changes()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new PackagesControlPanelState();

        var installedPackage = new ClusterPackageInfo()
        {
            Name = _dependency.Name,
            InstalledVersion = "1.0.0",
        };
        state.InstalledQueryable = new List<ClusterPackageInfo>() { installedPackage }.AsQueryable();

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<PackagesControlPanelState>>();

        // Act
        await saveHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        await _packageService.Received(0).GetPackages(null, Arg.Any<CancellationToken>());
    }
}
