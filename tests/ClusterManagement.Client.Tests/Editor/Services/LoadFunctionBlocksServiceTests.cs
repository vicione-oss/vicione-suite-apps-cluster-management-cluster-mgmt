using Bunit;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Public.Designs;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;
using Xunit;
using ClusterManagement.Client.Services;

namespace ClusterManagement.Client.Tests.Editor.Services;

public class LoadFunctionBlocksServiceTests
{
    private readonly IClusterService _clusterServiceMock = Substitute.For<IClusterService>();
    private readonly IDesignProvider _designProviderMock = Substitute.For<IDesignProvider>();

    private LoadFunctionBlocksService SetupService(TestContext ctx)
    {
        ctx.SetupSuiteServices();
        ctx.Services.AddClusterEditor(s => Substitute.For<IRulesetProvider>());
        ctx.Services.AddSingleton(_clusterServiceMock);
        ctx.Services.AddSingleton(_designProviderMock);
        ctx.Services.AddScoped<LoadFunctionBlocksService>();
        return ctx.Services.GetRequiredService<LoadFunctionBlocksService>();
    }

    [Fact]
    public async Task Load_package_references_should_fill_service_property()
    {
        // Arrange
        var dependencies = new List<ClusterDependency>()
        {
            new()
            {
                Name = "Test.Me.Good",
                Version = new Version("0.0.1")
            },
            new()
            {
                Name = "Test.Me.Better",
                Version = new Version("0.1.2")
            },
        };

        using var ctx = new TestContext();
        var service = SetupService(ctx);

        _designProviderMock.GetClusterDependencies()
            .Returns(dependencies);

        // Act
        await service.LoadPackageReferences();

        // Assert
        service.Dependencies.Should().NotBeNull().And.BeEquivalentTo(dependencies);
    }

    [Fact]
    public async Task Load_package_references_should_catch_errors()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);

        _designProviderMock.GetClusterDependencies().ThrowsAsync(new InvalidDataException());

        // Act
        await service.LoadPackageReferences();

        // Assert
        service.Dependencies.Should().BeNull();
    }

    [Fact]
    public async Task Load_selected_function_blocks_should_fill_cluster_editor()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);
        service.DisplayDialog = true;

        var dependency = new ClusterDependency()
        {
            Name = "Test.Me.Good",
            Version = new Version("0.0.1")
        };

        // Act
        service.SelectedDependencies =
        [
            dependency
        ];
        await service.LoadSelectedFunctionBlocks();

        // Assert
        service.DisplayDialog.Should().BeFalse();
    }

    [Fact]
    public async Task Load_selected_function_blocks_should_do_nothing_if_no_package_is_selected()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);
        service.DisplayDialog = true;

        // Act
        await service.LoadSelectedFunctionBlocks();

        // Assert
        service.DisplayDialog.Should().BeFalse();
        _ = _designProviderMock.Received(0).GetFunctionBlockDesignIds(Arg.Any<ClusterDependency[]>());
    }


    [Fact]
    public async Task Load_selected_function_blocks_should_catch_errors()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);
        service.DisplayDialog = true;
        var dependency = new ClusterDependency()
        {
            Name = "Test.Me.Good",
            Version = new Version("0.0.1")
        };

        _designProviderMock.GetFunctionBlockDesignIds(dependency).ThrowsAsync(new InvalidDataException());

        // Act
        service.SelectedDependencies =
        [
            dependency
        ];
        await service.LoadSelectedFunctionBlocks();

        // Assert
        service.DisplayDialog.Should().BeFalse();
    }
}
