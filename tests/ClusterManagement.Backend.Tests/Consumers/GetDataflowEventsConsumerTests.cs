using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Consumers;

public class GetDataflowEventsConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    private void SetupConsumer(IBusRegistrationConfigurator cfg, IDependencyResolver? dependencyResolver = null)
    {
        var designProvider = Substitute.For<IDesignProvider>();
        designProvider.CreateResolver().Returns(dependencyResolver ?? Substitute.For<IDependencyResolver>());

        cfg.AddConsumer<GetDataflowEventsConsumer>();
        cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
        cfg.AddSingleton(_snapshotStore);
        cfg.AddSingleton(Substitute.For<ILogger<GetDataflowEventsConsumer>>());
        cfg.AddSingleton(_ => designProvider ?? Substitute.For<IDesignProvider>());
    }

    public static TheoryData<GetDataflowEventsRequest> UseCases()
    => [
        new GetDataflowEventsRequest(),
        new GetDataflowEventsRequest() { ClusterId = Guid.NewGuid() },
        new GetDataflowEventsRequest() { DataflowId = Guid.NewGuid() },
    ];

    [Theory]
    [MemberData(nameof(UseCases))]
    public async Task Request_without_any_id_should_return_error(GetDataflowEventsRequest request)
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Request_with_unknown_cluster_id_should_return_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var request = new GetDataflowEventsRequest() { ClusterId = Guid.NewGuid() };

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Request_with_valid_cluster_id_should_not_return_error()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster();
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg, builder.DependencyResolver));
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();

        _snapshotStore.SetupLoad(builder.Cluster);

        var cluster = dbContext.SaveActiveClusterVersion(builder.Cluster);
        var request = new GetDataflowEventsRequest() { ClusterId = cluster.Id };

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_with_cluster_id_should_return_event_enabled_connectors()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        AddDataflowWithTwoEventEnabledConnectors(builder, out var connectors);

        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg, builder.DependencyResolver));

        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        dbContext.SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        var request = new GetDataflowEventsRequest() { ClusterId = builder.Cluster.Id };

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.Results.Should().HaveCount(4, "2 inputs + 2 outputs");
    }

    [Fact]
    public async Task Request_with_dataflow_id_should_return_event_enabled_connectors()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        var dataflow = AddDataflowWithTwoEventEnabledConnectors(builder, out var connectors);

        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg, builder.DependencyResolver));

        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        dbContext.SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        var request = new GetDataflowEventsRequest() { ClusterId = builder.Cluster.Id, DataflowId = dataflow.Id };

        // Act
        // maybe report this issue to masstransit - on debug you'll get this:
        // TypeLoadException: Method 'set_RequestError' in type 'MassTransit.DynamicInternal.Sdk.Messaging.IResponse' from assembly 'Sdk.MessagingMassTransit.DynamicInternal42b0649b3bd9426792d09eaacd0129c6, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' does not have an implementation.
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.Results.Should().HaveCount(2);
        response.Results.Should().ContainSingle(k => k.EventId == connectors[0].Id);
        response.Results.Should().ContainSingle(k => k.EventId == connectors[1].Id);
    }

    [Fact]
    public async Task Events_name_their_function_block_connector_dataflow_and_containers()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        var fbDesign = builder.Cache.FunctionBlockDesigns.First();
        var dataflow = builder.Editors.Cluster.AddDataflow("Line 3", new Version(0, 0, 1));
        var pump = builder.Editors.Container.AddContainer(dataflow.Root, "Pump 1");
        var vibration = builder.Editors.Container.AddContainer(pump, "Vibration");
        var functionBlock = builder.Editors.Container.AddFunctionBlock(vibration, fbDesign.Key, "Crest");
        var input = functionBlock.Inputs.First();
        builder.Editors.Connector.SetEventEnabled(true, input);

        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg, builder.DependencyResolver));
        tester.Services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        var request = new GetDataflowEventsRequest() { ClusterId = builder.Cluster.Id, DataflowId = dataflow.Id };

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        var dataflowEvent = response.Results.Should().ContainSingle().Which;
        dataflowEvent.FunctionBlockId.Should().Be(functionBlock.Id);
        dataflowEvent.FunctionBlockName.Should().Be("Crest");
        dataflowEvent.ConnectorName.Should().Be(input.Name);
        dataflowEvent.DataflowName.Should().Be("Line 3");
        dataflowEvent.ContainerPath.Should().Equal("Pump 1", "Vibration");
    }

    [Fact]
    public async Task A_function_block_directly_in_the_dataflow_has_no_container_path()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        var dataflow = AddDataflowWithTwoEventEnabledConnectors(builder, out _);

        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg, builder.DependencyResolver));
        tester.Services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        var request = new GetDataflowEventsRequest() { ClusterId = builder.Cluster.Id, DataflowId = dataflow.Id };

        // Act
        var response = await tester.TestRequest<GetDataflowEventsResponse, GetDataflowEventsRequest>(request);

        // Assert
        response.Results.Should().HaveCount(2).And.OnlyContain(e => e.DataflowName == "Second" && e.ContainerPath.Count == 0);
    }

    private static Dataflow AddDataflowWithTwoEventEnabledConnectors(ClusterBuilder builder, out IConnector[] connectors)
    {
        var fbDesign = builder.Cache.FunctionBlockDesigns.First();
        var dataflow = builder.Editors.Cluster.AddDataflow("Second", new Version(0, 0, 1));
        var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, fbDesign.Key, "AddedFb");
        connectors = [functionBlock.Inputs.First(), functionBlock.Outputs.First()];
        builder.Editors.Connector.SetEventEnabled(true, connectors);

        return dataflow;
    }
}
