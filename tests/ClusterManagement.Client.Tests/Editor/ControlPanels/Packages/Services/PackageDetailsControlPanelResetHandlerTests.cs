using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Services;

public sealed class PackageDetailsControlPanelResetHandlerTests
{
    private readonly ClusterDependency _dependency = new ClusterDependency
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddScoped<IControlPanelResetHandler<PackageDetailsControlPanelState>, PackageDetailsControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_set_version_to_install_from_pending_operation()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var packageInfo = new ClusterPackageInfo
        {
            Name = _dependency.Name,
            PendingOperation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install)
        };

        var state = new PackageDetailsControlPanelState()
        {
            PackageInfo = packageInfo,
            VersionToInstall = "5.2.3"
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<PackageDetailsControlPanelState>>();

        // Act
        await saveHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.VersionToInstall.Should().Be(packageInfo.PendingOperation.Dependency.Version.ToString());
    }

    [Fact]
    public async Task Should_set_version_to_install_empty_for_pending_uninstall_operation()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var packageInfo = new ClusterPackageInfo
        {
            Name = _dependency.Name,
            PendingOperation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Uninstall)
        };

        var state = new PackageDetailsControlPanelState()
        {
            PackageInfo = packageInfo,
            VersionToInstall = "5.2.3"
        };
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<PackageDetailsControlPanelState>>();

        // Act
        await saveHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Assert
        state.VersionToInstall.Should().BeEmpty();
    }
}
