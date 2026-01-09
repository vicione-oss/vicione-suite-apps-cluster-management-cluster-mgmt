using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Testing.Client;
using ViciOne.Cluster.Builder;
using Xunit;

namespace ClusterManagement.Client.Tests.Editor.Services;

public class SaveClusterServiceTests
{
    private readonly IClusterService _clusterServiceMock = Substitute.For<IClusterService>();
    private readonly IDependencyResolver _dependencyResolverMock = Substitute.For<IDependencyResolver>();
    private readonly IDesignProvider _designProviderMock = Substitute.For<IDesignProvider>();

    private SaveClusterService SetupService(TestContext ctx)
    {
        ctx.AddTestAuthorization();
        ctx.SetupSuiteServices();

        ctx.Services.AddSingleton(_clusterServiceMock);
        ctx.Services.AddSingleton(_designProviderMock);
        ctx.Services.AddSingleton<SaveClusterService>();
        return ctx.Services.GetRequiredService<SaveClusterService>();
    }

    [Fact]
    public void Save_request_by_editor_should_set_builder_and_display_dialog()
    {
        // Arrange
        using var builder = new ClusterBuilder(_dependencyResolverMock);

        using var ctx = new TestContext();
        var service = SetupService(ctx);

        // Act
        service.SaveRequested(builder);

        // Assert
        service.DisplayDialog.Should().BeTrue();
    }


    [Fact]
    public async Task Save_should_store_current_cluster()
    {
        // Arrange
        using var builder = new ClusterBuilder(_dependencyResolverMock);

        using var ctx = new TestContext();
        var service = SetupService(ctx);
        await service.SaveRequested(builder);

        // Act
        await service.Save();

        var publishOptions = new CommitClusterOptions
        {
            CleanupDependencies = service.StoreOptions.CleanupDependencies
        };

        // Assert
        _ = _clusterServiceMock.Received().CommitChange(builder.Cluster, publishOptions, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cluster_changed_event_should_hide_dialog()
    {
        // Arrange
        using var builder = new ClusterBuilder(_dependencyResolverMock);

        using var ctx = new TestContext();
        var service = SetupService(ctx);
        await service.SaveRequested(builder);
        await service.Save();
        service.DisplayDialog = true;

        var context = ClientContextFactory.Create(new ClusterChanged(Guid.NewGuid(), builder.Cluster.Id, CrudAction.Created, new Version()));

        // Act
        await service.Consume(context, CancellationToken.None);

        // Assert
        service.DisplayDialog.Should().BeFalse();
    }

    [Fact]
    public void Cluster_changed_event_not_correlated_should_be_ignored()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);
        service.DisplayDialog = true;

        var context = ClientContextFactory.Create(new ClusterChanged(Guid.NewGuid(), Guid.NewGuid(), CrudAction.Created, new Version()));

        // Act
        service.Consume(context, CancellationToken.None);

        // Assert
        service.DisplayDialog.Should().BeTrue();
    }

    [Fact]
    public async Task Cluster_error_event_should_display_dialog()
    {
        // Arrange
        using var builder = new ClusterBuilder(_dependencyResolverMock);
        var clusterId = Guid.NewGuid();
        var clusterVersion = new Version(0, 1, 1);
        var errorInfo = CreateErrorInfo(ClusterError.PublishFailed, clusterId, clusterVersion);

        using var ctx = new TestContext();
        var service = SetupService(ctx);
        await service.SaveRequested(builder);
        await service.Save();

        var context = ClientContextFactory.Create(new CommitClusterChangeFailed(clusterId, clusterId, clusterVersion, errorInfo));

        // Act
        await service.Consume(context, CancellationToken.None);

        // Assert
        service.DisplayDialog.Should().BeTrue();
    }

    [Fact]
    public async Task Cluster_error_event_not_correlated_should_be_ignored()
    {
        // Arrange
        using var ctx = new TestContext();
        var service = SetupService(ctx);
        var clusterId = Guid.NewGuid();
        var clusterVersion = new Version(0, 1, 1);
        var errorInfo = CreateErrorInfo(ClusterError.PublishFailed, clusterId, clusterVersion);
        var context = ClientContextFactory.Create(new CommitClusterChangeFailed(clusterId, clusterId, clusterVersion, errorInfo));

        // Act
        await service.Consume(context, CancellationToken.None);

        // Assert
        service.DisplayDialog.Should().BeFalse();
    }

    private static ErrorInfo CreateErrorInfo(int code, Guid? clusterId, Version? clusterVersion = null)
        => code switch
        {
            ClusterError.PublishFailed => new ErrorInfo(code,
                $"Failed to publish version {clusterVersion} of cluster {clusterId}"),
            ClusterError.DeleteFailed => new ErrorInfo(code,
                $"Unable to delete cluster {clusterId} from database"),
            ClusterError.ClusterIdUnknown => new ErrorInfo(code,
                $"Unable to find cluster {clusterId} in database"),
            ClusterError.NoClusterForVersion => new ErrorInfo(code,
                $"No cluster (id: {clusterId}) with version {clusterVersion} found!"),
            _ => new ErrorInfo(code, $"Unknown operation error ClusterManagement {clusterId}")
        };
}
