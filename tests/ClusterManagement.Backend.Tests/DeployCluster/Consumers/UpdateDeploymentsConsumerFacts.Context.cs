using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions.Execution;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public partial class UpdateDeploymentsConsumerFacts : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private static readonly Guid s_functionBlockDesignId = Guid.NewGuid();
    private static readonly Guid s_functionBlockOutputDesignId = Guid.NewGuid();

    private readonly IMqttBrokerConnectionCache _brokerConnectionCache;
    private readonly EngineDbContextSqlite _engineDbContext;
    private readonly IEngineHostPoolCommunication _poolCommunication;
    private readonly IEngineHostCommunication _hostCommunication;
    private readonly IDesignProvider _designProvider;
    private readonly ISuiteRulesetProvider _rulesetProvider;
    private readonly IInstanceInformationProvider _instanceInformationProvider;
    private readonly InMemoryLogger<UpdateDeploymentsConsumer> _logger;
    private readonly IMqttClient _mqttClient;
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider;
    private readonly MockFileSystem _fileSystemMock = new();
    private readonly IInstanceInformation _instanceInformation;
    private readonly ClusterBuilder _clusterBuilder;
    private readonly Guid _correlationId = Guid.NewGuid();
    private readonly List<object> _events = [];
    private CorrelatedBy<Guid>? _result;

    public UpdateDeploymentsConsumerFacts()
    {
        var functionBlockDesign = CreateFunctionBlockDesign();
        var dependencyDesigns = ClusterDependencyPackageFactory.CreateDependencyDesigns([functionBlockDesign,]);

        var functionBlockDesignResolver = new ClusterDependencyResolver(dependencyDesigns);
        _clusterBuilder = new(functionBlockDesignResolver);

        _brokerConnectionCache = Substitute.For<IMqttBrokerConnectionCache>();
        _brokerConnectionCache.GetMqttConnection(Arg.Any<CancellationToken>())
            .Returns(new Sdk.Connections.Contracts.MqttConnection());

        var optionsBuilder = new DbContextOptionsBuilder<EngineDbContextSqlite>()
            .UseSqlite("DataSource=:memory:");
        _engineDbContext = new EngineDbContextSqlite(optionsBuilder.Options);
        _engineDbContext.Database.OpenConnection();
        _engineDbContext.Database.EnsureCreated();

        _poolCommunication = Substitute.For<IEngineHostPoolCommunication>();
        _poolCommunication.GetStates(Arg.Any<CancellationToken>()).Returns(_engineHostsStates);
        _poolCommunication.StopHost(Arg.Do<Guid>(id =>
        {
            var engineHostDeploymentStates = _engineHostsDeploymentStates[id];
            foreach (var key in engineHostDeploymentStates.Keys)
                engineHostDeploymentStates[key] = EngineState.Stopped;
        }), Arg.Any<CancellationToken>());
        _hostCommunication = Substitute.For<IEngineHostCommunication>();

        _designProvider = Substitute.For<IDesignProvider>();
        _designProvider.CreateResolver().Returns(functionBlockDesignResolver);

        _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();

        _instanceInformation = Substitute.For<IInstanceInformation>();
        _instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
        _instanceInformationProvider.Local.Returns(_instanceInformation);

        _logger = new InMemoryLogger<UpdateDeploymentsConsumer>();
        _mqttClient = Substitute.For<IMqttClient>();
        _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    }

    private static ViciOne.Core.Dataflow.DataModel.FunctionBlockDesign CreateFunctionBlockDesign()
    {
        var functionBlockDesign = FunctionBlockDesignBuilder.Create(s_functionBlockDesignId);
        functionBlockDesign.SystemInputs.Clear();
        functionBlockDesign.SystemOutputs.Clear();
        functionBlockDesign.AddOutput<double>(s_functionBlockOutputDesignId, "Value");
        return functionBlockDesign;
    }

    protected override void Dispose(bool disposing)
    {
        _mqttClient.Dispose();
        _engineDbContext.Dispose();
        _clusterBuilder.Dispose();

        base.Dispose(disposing);
    }

    internal void Given_configured_suite()
    {
        Given_cluster_node_group();
        Given_cluster_node();
        Given_cluster_application();
    }

    internal void Given_cluster_node_group()
        => _clusterBuilder.Editors.Cluster.AddNodeGroup();

    internal void Given_cluster_node()
        => _clusterBuilder.Editors.NodeGroup.AddNode(_clusterBuilder.Cache.ClusterNodeGroups.Single());

    internal void Given_cluster_application()
    {
        var application = _clusterBuilder.Editors.Node.AddApplication(_clusterBuilder.Cache.ClusterNodes.Single(), ClusterApplicationType.CoreOsStandalone);
        _instanceInformation.Id.Returns(application.Id);
    }

    internal async Task When_updating_deployments(string? version = default)
    {
        _events.Clear();

        var clusterVersion = version is not null ? new Version(version) : _clusterBuilder.Cluster.Version;
        var clusterCompressed = ClusterSerializer.Compress(_clusterBuilder.Cluster);
        var clusterInfo = new ClusterSnapshotInfo(_clusterBuilder.Cluster.Id, clusterVersion, _clusterBuilder.Cluster.Name);

        var cancellationToken = TestContext.Current.CancellationToken;
        var consumeContext = Substitute.For<ConsumeContext<UpdateDeployments>>();
        consumeContext.Message.Returns(new UpdateDeployments(clusterCompressed, _correlationId, _clusterBuilder.Cluster.Id));
        consumeContext.CorrelationId.Returns(_correlationId);
        consumeContext.CancellationToken.Returns(cancellationToken);
        consumeContext.When(x => x.Publish(Arg.Any<Arg.AnyType>(), Arg.Is(cancellationToken))).Do(x => _events.Add(x[0]));

        UpdateDeploymentsConsumer consumer = new(_instanceInformationProvider, _engineDbContext, _poolCommunication,
            _hostCommunication, _brokerConnectionCache, _designProvider, Substitute.For<IClusterDbContext>(), _workspaceProvider,
            _rulesetProvider, _fileSystemMock, _logger, _mqttClient);
        await consumer.Consume(consumeContext);

        _result = _events.OfType<CorrelatedBy<Guid>>().SingleOrDefault();
    }

    internal void Then_update_succeeded()
    {
        using AssertionScope scope = new();
        scope.AppendTracing(_logger.LastMessage);
        if (_result is ClusterNodeDeploymentsUpdateFailedEvent failed)
            scope.AppendTracing(failed.ErrorInfo.ToString());
        var updatedEvent = _result.Should().NotBeNull()
            .And.BeOfType<ClusterNodeDeploymentsUpdatedEvent>()
            .Which.CorrelationId.Should().Be(_correlationId);
        Then_no_errors_were_logged();
    }

    internal void Then_update_failed(int errorCode)
    {
        using AssertionScope scope = new();
        scope.AppendTracing(_logger.LastMessage);
        _result.Should().NotBeNull().And.BeOfType<ClusterNodeDeploymentsUpdateFailedEvent>().Which.ErrorInfo.ErrorCode.Should().Be(errorCode);
        Then_errors_were_logged();
    }

    private void Then_event_was_published<T>(string because, Func<T, bool>? predicate = default) where T : class
    {
        var events = _events.OfType<T>();
        if (predicate is not null)
            events = events.Where(predicate);
        events.Should().NotBeEmpty(because);
    }

    private void Then_no_event_was_published<T>(string because, Func<T, bool>? predicate = default) where T : class
    {
        var events = _events.OfType<T>();
        if (predicate is not null)
            events = events.Where(predicate);
        events.Should().BeEmpty(because);
    }

    private void Then_errors_were_logged() => _logger.LogEntries.Should().Contain(static e => LogLevelIsErrorOrCritical(e));

    private void Then_no_errors_were_logged() => _logger.LogEntries.Should().NotContain(static e => LogLevelIsErrorOrCritical(e));

    private static bool LogLevelIsErrorOrCritical(InMemoryLogEntry e) => e.LogLevel is LogLevel.Error or LogLevel.Critical;
}
