using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Engines.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;

namespace ClusterManagement.Backend.Tests.Engines.Consumers;

public class GetFunctionBlockParametersConsumerFacts : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    private readonly ISuiteMediator _mediator;
    private readonly Guid _clusterId = Guid.NewGuid();
    private readonly Guid _functionBlockId = Guid.NewGuid();

    public GetFunctionBlockParametersConsumerFacts()
    {
        _mediator = Substitute.For<ISuiteMediator>();

        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetFunctionBlockParametersConsumer>();
            cfg.AddSingleton<IClusterDbContext>(TestDbContext);
            cfg.AddSingleton(_mediator);
            cfg.AddSingleton(_snapshotStore);
        };
    }

    [Fact]
    public async Task Responds_with_error_if_cluster_is_not_active()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act
        var response = await tester.TestRequest<GetFunctionBlockParametersResponse, GetFunctionBlockParametersRequest>(
            new GetFunctionBlockParametersRequest(_clusterId, []));

        // Assert
        Assert.NotNull(response.RequestError);
        response.RequestError.ErrorCode.Should().Be(ClusterError.ClusterNotActive);
        response.RequestError.Message.Should().MatchEquivalentOf($"*not*load*active*cluster*'{_clusterId}'*");
    }

    [Fact]
    public async Task Responds_with_error_if_cluster_could_not_be_found()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        TestDbContext.SaveActiveClusterVersion(new ViciOne.Cluster.Model.Cluster() { Id = _clusterId, Version = new Version(1, 0, 0) });

        // Act
        var response = await tester.TestRequest<GetFunctionBlockParametersResponse, GetFunctionBlockParametersRequest>(
            new GetFunctionBlockParametersRequest(_clusterId, []));

        // Assert
        Assert.NotNull(response.RequestError);
        response.RequestError.ErrorCode.Should().Be(ClusterError.ClusterIdUnknown);
        response.RequestError.Message.Should().MatchEquivalentOf($"*not*find*cluster*'{_clusterId}'*");
    }

    [Fact(Skip = "instance dependent requests need a extension soon will be added to sdk with version > 0.25.0")]
    public async Task Requests_and_returns_settings_and_variables_values_from_applications()
    {
        // Arrange
        var functionBlockDesign = FunctionBlockDesignBuilder.Create(Guid.NewGuid());
        functionBlockDesign.AddSetting<int>(Guid.NewGuid());
        functionBlockDesign.AddVariable<bool>(Guid.NewGuid());
        var functionBlockDesignResolver = new ClusterDependencyResolver(ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]));
        using ClusterBuilder builder = new(functionBlockDesignResolver);
        builder.Cluster.Id = _clusterId;

        var engine = builder.CreateTopologyToEngine("Engine A");
        var dataflow = builder.Cluster.Dataflows.First();
        var functionBlock = builder.Editors.Container.AddFunctionBlock(dataflow.Root, functionBlockDesign.Id, id: _functionBlockId);
        builder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);

        TestDbContext.SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        RetrieveFunctionBlockParametersResponse retrieveResponse = new(
        [
            new(_functionBlockId, new Dictionary<Guid, FunctionBlockParameterValue>
            {
                { functionBlock.Settings[0].Id, new("int", "123") },
                { functionBlock.PersistentVariables[0].Id, new("bool", "true") },
            }),
        ]);
        _mediator.SetupRequest<RetrieveFunctionBlockParametersRequest, RetrieveFunctionBlockParametersResponse>(null, retrieveResponse, Guid.Empty);

        await using var tester = new MassTransitTester(_configureServices);

        // Act
        var response = await tester.TestRequest<GetFunctionBlockParametersResponse, GetFunctionBlockParametersRequest>(
            new GetFunctionBlockParametersRequest(_clusterId, [_functionBlockId,]));

        // Assert
        response.RequestError.Should().BeNull();
        response.FunctionBlockParameters.Should().BeEquivalentTo(new FunctionBlockParameterValues[]
        {
            new(_functionBlockId, new Dictionary<Guid, FunctionBlockParameterValue>()
            {
                { functionBlock.Settings[0].Id, new("int", "123") },
                { functionBlock.PersistentVariables[0].Id, new("bool", "true") },
            }),
        });
    }
}
