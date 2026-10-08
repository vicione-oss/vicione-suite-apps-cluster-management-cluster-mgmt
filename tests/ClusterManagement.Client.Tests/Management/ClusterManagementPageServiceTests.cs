using Bunit;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterManagementPageServiceTests
{
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();


    [Fact(Skip = "To be implemented..")]
    public async Task Query_cluster_versions_should_()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        ctx.SetupSuiteServices();

        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();

        // Act
        await service.QueryClusterVersions(Guid.NewGuid(), Xunit.TestContext.Current.CancellationToken);

        // Assert
        //service.Builder.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_update_changed()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateChanged(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0), "Ready") { Change = ClusterUpdateChange.Update };
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateChanged>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateChanged))
            .WithArgs<ClusterUpdateChanged>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_completed_event()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0), TimeSpan.FromSeconds(19));
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateCompleted))
            .WithArgs<ClusterUpdateCompleted>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_update_failed()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateFailed(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0));
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateFailed>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateFailed))
            .WithArgs<ClusterUpdateFailed>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_update_rejected_for_the_own_deployment()
    {
        // Arrange
        using var ctx = new BunitContext();
        IUiMediator mediator = default!;
        ctx.SetupSuiteServices(setup => mediator = setup.ClientMediator);
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        await service.DeployCluster(Guid.NewGuid(), new Version(0, 1, 2), CancellationToken.None);
        var command = mediator.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<DeployCluster>().Single();
        var statusEvent = new ClusterUpdateRejected(command.CorrelationId, command.ClusterId, new Version(0, 1, 0), new Version(0, 1, 1), new Version(0, 1, 2));
        using var monitor = service.Monitor();

        // Act
        await service.Consume(new ClientContext<ClusterUpdateRejected>(statusEvent, command.CorrelationId), CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateRejected))
            .WithArgs<ClusterUpdateRejected>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_ignore_the_rejected_deployment_of_another_session()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        await service.DeployCluster(Guid.NewGuid(), new Version(0, 1, 2), CancellationToken.None);
        var correlationId = Guid.NewGuid();
        var statusEvent = new ClusterUpdateRejected(correlationId, Guid.NewGuid(), new Version(0, 1, 0), new Version(0, 1, 1), new Version(0, 1, 3));
        using var monitor = service.Monitor();

        // Act
        await service.Consume(new ClientContext<ClusterUpdateRejected>(statusEvent, correlationId), CancellationToken.None);

        // Assert
        monitor.Should().NotRaise(nameof(ClusterManagementPageService.OnClusterUpdateRejected));
    }
}
