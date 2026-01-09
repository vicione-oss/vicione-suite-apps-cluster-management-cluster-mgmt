using System.Globalization;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Engines.Extensions;
using ClusterManagement.Public.Events;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ViciOne.Cluster.Model.Extensions;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public partial class UpdateDeploymentsConsumerFacts
{
    private readonly Dictionary<Guid, Guid> _engineDeploymentIdentifiers = [];
    private readonly Dictionary<Guid, EngineHostState> _engineHostsStates = [];
    private readonly Dictionary<Guid, Dictionary<Guid, EngineState>> _engineHostsDeploymentStates = [];
    private readonly Dictionary<Guid, Guid> _functionBlocksOutput = [];

    internal void Given_user_added_engineHost(Guid id, bool elevatedPrivileges = false)
    {
        var engineHost = _clusterBuilder.Editors.Application.AddEngineHost(_clusterBuilder.Cache.ClusterApplications.Single(), id.ToString(), id);
        engineHost.ElevatedPrivileges = elevatedPrivileges;
        _poolCommunication.GetState(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(EngineHostState.NotFound);
        _engineHostsStates[id] = EngineHostState.NotFound;
        _engineHostsDeploymentStates[id] = [];
        _hostCommunication.GetDeploymentStates(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(_engineHostsDeploymentStates[id]);
    }

    internal void Given_engineHost_is_known(Guid id, bool elevatedPrivileges = false)
    {
        _engineDbContext.EngineHosts.Add(new() { Id = id, ElevatedPrivileges = elevatedPrivileges, });
        _engineDbContext.SaveChanges();
        _poolCommunication.GetState(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(EngineHostState.NotFound);
        _engineHostsStates[id] = EngineHostState.NotFound;
        _engineHostsDeploymentStates[id] = [];
        _hostCommunication.GetDeploymentStates(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(_engineHostsDeploymentStates[id]);
    }

    internal void Given_deploy_engineHost_will_fail(Guid id)
        => _poolCommunication.DeployHost(Arg.Is(id), Arg.Any<CancellationToken>()).ThrowsAsync<Exception>();

    internal void Given_engineHost_is_deployed(Guid id)
    {
        _poolCommunication.GetState(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(EngineHostState.Stopped);
        _engineHostsStates[id] = EngineHostState.Stopped;
        _engineHostsDeploymentStates[id] = [];
        _hostCommunication.GetDeploymentStates(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(_engineHostsDeploymentStates[id]);
    }

    internal void Given_engineHost_has_different_appsettings(Guid id)
        => _poolCommunication.CompareHostAppSettings(Arg.Is(id), Arg.Any<EngineHostAppSettings>(), Arg.Any<CancellationToken>()).Returns(-1);

    internal void Given_set_appsettings_will_fail(Guid id)
        => _poolCommunication.SetHostAppSettings(Arg.Is(id), Arg.Any<EngineHostAppSettings>(), Arg.Any<CancellationToken>()).ThrowsAsync<Exception>();

    internal void Given_engineHost_is_started(Guid id)
    {
        _poolCommunication.GetState(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
        _engineHostsStates[id] = EngineHostState.Started;
        _engineHostsDeploymentStates[id] = [];
        _hostCommunication.GetDeploymentStates(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(_engineHostsDeploymentStates[id]);
    }

    internal void Given_start_engineHost_will_fail(Guid id)
        => _poolCommunication.StartHost(Arg.Is(id), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync<Exception>();

    internal void Given_stop_engineHost_will_fail(Guid id)
        => _poolCommunication.StopHost(Arg.Is(id), Arg.Any<CancellationToken>()).ThrowsAsync<Exception>();

    internal void Given_tear_down_engineHost_will_fail(Guid id)
        => _poolCommunication.TearDownHost(Arg.Is(id), Arg.Any<CancellationToken>()).ThrowsAsync<Exception>();

    internal void Given_user_added_engine(Guid engineId, Guid hostId)
    {
        var host = _clusterBuilder.Cache.EngineHosts.Single(x => x.Id == hostId);
        _clusterBuilder.Editors.EngineHost.AddEngine(host, engineId.ToString(), engineId);
        var deploymentIdentifier = EnsureDeploymentIdentifier(engineId);
        _hostCommunication.DeployEngine(Arg.Is(hostId), Arg.Is<Workflow>(_ => _.Engine.Id == engineId), Arg.Any<CancellationToken>())
            .Returns(deploymentIdentifier);
        _hostCommunication.StartEngine(Arg.Is(hostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>()).Returns(true);
    }

    internal void Given_user_disabled_engine(Guid engineId)
    {
        var engine = _clusterBuilder.Cache.EngineGuids[engineId];
        _clusterBuilder.Editors.Engine.SetEnabled(engine, false);
    }

    internal void Given_engine_is_known(Guid engineHostId, Guid engineId, bool shouldRun = false)
    {
        var deploymentIdentifier = EnsureDeploymentIdentifier(engineId);
        _engineDbContext.EngineDeployments.Add(new()
        {
            Id = deploymentIdentifier,
            EngineHostId = engineHostId,
            EngineId = engineId,
            Version = _clusterBuilder.Cluster.Version.ToString(),
            ShouldRun = shouldRun,
            Checksum = new WorkflowComparer().GetHashCode(GetEmptyWorkflow(engineHostId, engineId)),
        });
        _engineDbContext.SaveChanges();
        _hostCommunication.GetEngineState(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>())
            .Throws<InvalidOperationException>();
        _hostCommunication.StopEngine(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    internal void Given_deploy_engine_will_fail(Guid engineHostId, Guid engineId)
        => _hostCommunication.DeployEngine(Arg.Is(engineHostId), Arg.Is<Workflow>(_ => _.Engine.Id == engineId), Arg.Any<CancellationToken>())
        .ThrowsAsync<Exception>();

    internal void Given_engine_is_deployed(Guid engineHostId, Guid engineId)
    {
        var deploymentIdentifier = EnsureDeploymentIdentifier(engineId);
        _hostCommunication.GetEngineState(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>())
            .Returns(EngineState.Stopped);
        _engineHostsDeploymentStates[engineHostId][deploymentIdentifier] = EngineState.Stopped;
        _hostCommunication.StartEngine(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    internal void Given_engine_has_functionBlock(Guid engineId, Guid functionBlockId)
    {
        var deploymentIdentifier = EnsureDeploymentIdentifier(engineId);
        var engineDeployment = _engineDbContext.EngineDeployments.FirstOrDefault(ed => ed.Id == deploymentIdentifier);
        if (engineDeployment is not null)
        {
            var workflow = GetEmptyWorkflow(engineDeployment.EngineHostId, engineId);
            var containerId = _clusterBuilder.Cluster.Dataflows.First().Root.Id;
            workflow.ClusterDependencies.Add(new("TestDependency", "0.0.1"));
            workflow.Containers.Add(new()
            {
                Id = containerId,
                Name = "Root",
            });
            var functionBlockOutputId = EnsureOutputId(functionBlockId);
            workflow.FunctionBlocks.Add(new()
            {
                ContainerId = containerId,
                CycleFrequency = 1,
                DesignId = s_functionBlockDesignId,
                Id = functionBlockId,
                Name = functionBlockId.ToString(),
                Connectors =
                [
                    new()
                    {
                        DesignId = s_functionBlockOutputDesignId,
                        Id = functionBlockOutputId,
                        InitialValue = TypedObject.CreateOptional(0.0),
                        MarkAsChangedOnlyIfNotEqual = true,
                    },
                ],
            });
            workflow.ExternalConnections.Add(new()
            {
                Name = "Suite MQTT Broker",
                Communication = new()
                {
                    Id = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A",
                    Properties =
                    [
                        new("Host") { Value = TypedObject.CreateOptional(string.Empty), },
                        new("Port") { Value = TypedObject.CreateOptional(1883), },
                        new("Username") { Value = TypedObject.CreateOptional(string.Empty), },
                        new("Password") { Value = TypedObject.CreateOptional(string.Empty), },
                        new("MaxPendingMessages") { Value = TypedObject.CreateOptional(10_000), },
                        new("QualityOfService") { Value = TypedObject.CreateOptional((short)0), },
                        new("Retain") { Value = TypedObject.CreateOptional(true), },
                    ],
                },
                OutgoingLinks =
                [
                    new()
                    {
                        ConnectorId = functionBlockOutputId,
                        RemoteId = functionBlockOutputId,
                    },
                ],
            });
            engineDeployment.Checksum = new WorkflowComparer().GetHashCode(workflow);
            engineDeployment.MqttRetainTopics.Add(functionBlockOutputId);
            _engineDbContext.SaveChanges();
        }
    }

    private Guid EnsureOutputId(Guid functionBlockId)
    {
        if (!_functionBlocksOutput.TryGetValue(functionBlockId, out var outputId))
        {
            outputId = Guid.NewGuid();
            _functionBlocksOutput.Add(functionBlockId, outputId);
        }
        return outputId;
    }

    private Workflow GetEmptyWorkflow(Guid engineHostId, Guid engineId) => new()
    {
        Engine = new()
        {
            Id = engineId,
            CycleTime = 100,
            Name = engineId.ToString(),
            LogLevel = ViciOne.Cluster.Builder.EngineDefaults.LogLevel,
        },
        PersistenceCommunication = new()
        {
            Username = string.Empty,
            Password = string.Empty,
            TcpPort = 1883,
            QualityOfService = 1,
            Retain = true,
        },
        RuntimeFunctionsCommunication = new()
        {
            Username = string.Empty,
            Password = string.Empty,
            TcpPort = 1883,
            QualityOfService = 2,
            Retain = false,
        },
        EngineLogCommunication = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? new()
            {
                Id = "96B50074-1DF9-402C-90CF-AC9894C8E243",
                Properties =
                [
                    new("MetaDataFields")
                    {
                        Value = TypedObject.CreateMandatory(new Dictionary<string, object?>()
                        {
                            { "ENGINE_ID", engineId },
                            { "ENGINE_NAME", engineId.ToString() },
                            { "ENGINE_HOST_NAME", engineHostId.ToString() },
                            { "ENGINE_HOST_ID", engineHostId },
                            { "MODULE_ID", "ViciOne.Suite.ClusterManagement" },
                        }),
                    },
                    new("OutputTemplate") { Value = TypedObject.CreateMandatory($$"""{{engineId}} ({{engineId}}): [{SourceContext}] {Message:lj}{NewLine}{Exception}"""), },
                ],
            }
            : new()
            {
                Id = "E3601083-66AE-41FC-AC20-E05B1DD7936E",
                Properties =
                [
                    new("Path") { Value = TypedObject.CreateMandatory(_fileSystemMock.Path.Combine(_fileSystemMock.Path.GetFullPath(_fileSystemMock.GetEnginesDirectory(_workspaceProvider)), $"{engineId}.log")), },
                ],
            },
    };

    internal void Given_engine_is_started(Guid engineHostId, Guid engineId)
    {
        var deploymentIdentifier = EnsureDeploymentIdentifier(engineId);
        _hostCommunication.GetEngineState(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>())
            .Returns(EngineState.Running);
        _engineHostsDeploymentStates[engineHostId][deploymentIdentifier] = EngineState.Running;
        _hostCommunication.StopEngine(Arg.Is(engineHostId), Arg.Is(deploymentIdentifier), Arg.Any<CancellationToken>()).Returns(true);
    }

    private Guid EnsureDeploymentIdentifier(Guid engineId)
    {
        if (!_engineDeploymentIdentifiers.TryGetValue(engineId, out var deploymentIdentifier))
        {
            deploymentIdentifier = Guid.NewGuid();
            _engineDeploymentIdentifiers.Add(engineId, deploymentIdentifier);
        }
        return deploymentIdentifier;
    }

    internal void Given_start_engine_will_fail(Guid engineHostId, Guid engineId)
        => _hostCommunication.StartEngine(Arg.Is(engineHostId), Arg.Is(EnsureDeploymentIdentifier(engineId)), Arg.Any<CancellationToken>())
        .ThrowsAsync<Exception>();

    internal void Given_stop_engine_will_fail(Guid engineHostId, Guid engineId)
        => _hostCommunication.StopEngine(Arg.Is(engineHostId), Arg.Is(EnsureDeploymentIdentifier(engineId)), Arg.Any<CancellationToken>())
        .ThrowsAsync<Exception>();

    internal void Given_tear_down_engine_will_fail(Guid engineHostId, Guid engineId)
        => _hostCommunication.TearDownEngine(Arg.Is(engineHostId), Arg.Is(EnsureDeploymentIdentifier(engineId)), Arg.Any<CancellationToken>())
        .ThrowsAsync<Exception>();

    internal void Given_user_added_functionBlock(Guid engineId, Guid functionBlockId)
    {
        var dataflow = _clusterBuilder.Cluster.Dataflows.First();
        var functionBlock = _clusterBuilder.Editors.Container.AddFunctionBlock(dataflow.Root, s_functionBlockDesignId, id: functionBlockId);
        _clusterBuilder.Editors.FunctionBlock.SetName(functionBlock, functionBlockId.ToString());
        var output = functionBlock.GetConnectorByDesignId(s_functionBlockOutputDesignId) ?? throw new InvalidOperationException("Could not find output connector.");
        output.Id = EnsureOutputId(functionBlockId);
        _clusterBuilder.Editors.Connector.SetEventEnabled(true, output);
        var engine = _clusterBuilder.Cache.EngineGuids[engineId];
        _clusterBuilder.Editors.FunctionBlock.AssignEngine(engine, functionBlock);
    }

    internal Task Then_engineHost_gets_deployed(Guid id)
        => _poolCommunication.Received(1).DeployHost(id, Arg.Any<CancellationToken>());

    internal Task Then_engineHost_gets_not_deployed(Guid id)
        => _poolCommunication.DidNotReceive().DeployHost(id, Arg.Any<CancellationToken>());

    internal Task Then_no_engineHost_gets_deployed()
        => _poolCommunication.DidNotReceiveWithAnyArgs().DeployHost(default, default);

    internal Task Then_engineHost_gets_appsettings(Guid id)
        => _poolCommunication.Received(1).SetHostAppSettings(id, Arg.Any<EngineHostAppSettings>(), Arg.Any<CancellationToken>());

    internal Task Then_engineHost_gets_no_appsettings(Guid id)
        => _poolCommunication.DidNotReceive().SetHostAppSettings(id, Arg.Any<EngineHostAppSettings>(), Arg.Any<CancellationToken>());

    internal Task Then_no_engineHost_gets_appsettings()
        => _poolCommunication.DidNotReceiveWithAnyArgs().SetHostAppSettings(default, default!, default);

    internal void Then_engineHost_is_known(Guid id, bool elevatedPrivileges = false)
    {
        var engineHost = _engineDbContext.EngineHosts.AsNoTracking().SingleOrDefault(eh => eh.Id == id);
        engineHost.Should().NotBeNull();
        engineHost!.ElevatedPrivileges.Should().Be(elevatedPrivileges);
    }

    internal void Then_engineHost_is_not_known(Guid id)
        => _engineDbContext.EngineHosts.SingleOrDefault(eh => eh.Id == id).Should().BeNull();

    internal void Then_no_engineHost_is_known()
        => _engineDbContext.EngineHosts.Should().BeEmpty();

    internal Task Then_engineHost_gets_started(Guid id, bool elevatedPrivileges = false)
        => _poolCommunication.Received(1).StartHost(Arg.Is(id), Arg.Is(elevatedPrivileges), Arg.Any<CancellationToken>());

    internal Task Then_engineHost_gets_not_started(Guid id)
        => _poolCommunication.DidNotReceive().StartHost(Arg.Is(id), Arg.Any<bool>(), Arg.Any<CancellationToken>());

    internal Task Then_no_engineHost_gets_started()
        => _poolCommunication.DidNotReceiveWithAnyArgs().StartHost(default, default, default);

    internal Task Then_engineHost_gets_stopped(Guid id)
        => _poolCommunication.Received(1).StopHost(id, Arg.Any<CancellationToken>());

    internal Task Then_engineHost_gets_not_stopped(Guid id)
        => _poolCommunication.DidNotReceive().StopHost(id, Arg.Any<CancellationToken>());

    internal Task Then_no_engineHost_gets_stopped()
        => _poolCommunication.DidNotReceiveWithAnyArgs().StopHost(default, default);

    internal Task Then_engineHost_gets_torn_down(Guid id)
        => _poolCommunication.Received(1).TearDownHost(id, Arg.Any<CancellationToken>());

    internal Task Then_engineHost_gets_not_torn_down(Guid id)
        => _poolCommunication.DidNotReceive().TearDownHost(id, Arg.Any<CancellationToken>());

    internal Task Then_no_engineHost_gets_torn_down()
        => _poolCommunication.DidNotReceiveWithAnyArgs().TearDownHost(default, default);

    internal void Then_engine_is_known(Guid engineHostId, Guid engineId, bool shouldRun = false)
    {
        var deployment = _engineDbContext.EngineDeployments.SingleOrDefault(ed => ed.EngineHostId == engineHostId && ed.EngineId == engineId);
        deployment.Should().NotBeNull();
        deployment!.ShouldRun.Should().Be(shouldRun);
    }

    internal void Then_engine_is_not_known(Guid engineHostId, Guid engineId)
        => _engineDbContext.EngineDeployments.SingleOrDefault(ed => ed.EngineHostId == engineHostId && ed.EngineId == engineId).Should().BeNull();

    internal void Then_no_engine_is_known()
        => _engineDbContext.EngineDeployments.Should().BeEmpty();

    internal Task Then_engine_gets_deployed(Guid engineHostId, Guid engineId)
        => _hostCommunication.Received(1).DeployEngine(
            Arg.Is(engineHostId), Arg.Is<Workflow>(_ => _.Engine.Id == engineId), Arg.Any<CancellationToken>());

    internal Task Then_engine_gets_not_deployed(Guid engineId)
        => _hostCommunication.DidNotReceive().DeployEngine(
            Arg.Any<Guid>(), Arg.Is<Workflow>(_ => _.Engine.Id == engineId), Arg.Any<CancellationToken>());

    internal Task Then_no_engine_gets_deployed()
        => _hostCommunication.DidNotReceiveWithAnyArgs().DeployEngine(default, default!, default);

    internal Task Then_engine_gets_started(Guid engineHostId, Guid engineId)
        => _hostCommunication.Received(1).StartEngine(
            Arg.Is(engineHostId), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_engine_gets_not_started(Guid engineId)
        => _hostCommunication.DidNotReceive().StartEngine(
            Arg.Any<Guid>(), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_no_engine_gets_started()
        => _hostCommunication.DidNotReceiveWithAnyArgs().StartEngine(default, default, default);

    internal void Then_engine_was_started(Guid engineId)
        => Then_event_was_published<EngineStarted>(BecauseEngine(engineId, Was, Started), e => e.EngineId == engineId);

    internal void Then_engine_was_not_started(Guid engineId)
        => Then_no_event_was_published<EngineStarted>(BecauseEngine(engineId, WasNot, Started), e => e.EngineId == engineId);

    internal void Then_no_engine_was_started()
        => Then_no_event_was_published<EngineStarted>(BecauseNoEngineWas(Started));

    internal Task Then_engine_gets_stopped(Guid engineHostId, Guid engineId)
        => _hostCommunication.Received(1).StopEngine(
            Arg.Is(engineHostId), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_engine_gets_not_stopped(Guid engineId)
        => _hostCommunication.DidNotReceive().StopEngine(
            Arg.Any<Guid>(), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_no_engine_gets_stopped()
        => _hostCommunication.DidNotReceiveWithAnyArgs().StopEngine(default, default, default);

    internal void Then_engine_was_stopped(Guid engineId)
        => Then_event_was_published<EngineStopped>(BecauseEngine(engineId, Was, Stopped), e => e.EngineId == engineId);

    internal void Then_engine_was_not_stopped(Guid engineId)
        => Then_no_event_was_published<EngineStopped>(BecauseEngine(engineId, WasNot, Stopped), e => e.EngineId == engineId);

    internal void Then_no_engine_was_stopped()
        => Then_no_event_was_published<EngineStopped>(BecauseNoEngineWas(Stopped));

    internal Task Then_engine_gets_torn_down(Guid engineHostId, Guid engineId)
        => _hostCommunication.Received(1).TearDownEngine(
            Arg.Is(engineHostId), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_engine_gets_not_torn_down(Guid engineId)
        => _hostCommunication.DidNotReceive().TearDownEngine(
            Arg.Any<Guid>(), Arg.Is(_engineDeploymentIdentifiers[engineId]), Arg.Any<CancellationToken>());

    internal Task Then_no_engine_gets_torn_down()
        => _hostCommunication.DidNotReceiveWithAnyArgs().TearDownEngine(default, default, default);

    internal void Then_engine_was_torn_down(Guid engineId)
        => Then_event_was_published<EngineTornDown>(BecauseEngine(engineId, Was, TornDown), e => e.EngineId == engineId);

    internal void Then_engine_was_not_torn_down(Guid engineId)
        => Then_no_event_was_published<EngineTornDown>(BecauseEngine(engineId, WasNot, TornDown), e => e.EngineId == engineId);

    internal void Then_no_engine_was_torn_down()
        => Then_no_event_was_published<EngineTornDown>(BecauseNoEngineWas(TornDown));

    private const string EngineEventReason = "the user assumes that the engine '{0}' has {1}been {2}";
    private const string NoEngineEventsReason = "the user assumes that no engine has been {0}";
    private const bool Was = true;
    private const bool WasNot = false;
    private const string Started = "started";
    private const string Stopped = "stopped";
    private const string TornDown = "torn down";
    private static string BecauseEngine(Guid engineId, bool existence, string actionName)
        => string.Format(CultureInfo.CurrentCulture, EngineEventReason, engineId, existence ? string.Empty : "not ", actionName);
    private static string BecauseNoEngineWas(string actionName)
        => string.Format(CultureInfo.CurrentCulture, NoEngineEventsReason, actionName);

    internal Task Then_topics_were_deleted()
        => _mqttClient.ReceivedWithAnyArgs(1).PublishAsync(default, default);

    internal Task Then_no_topics_were_deleted()
        => _mqttClient.DidNotReceiveWithAnyArgs().PublishAsync(default, default);
}
