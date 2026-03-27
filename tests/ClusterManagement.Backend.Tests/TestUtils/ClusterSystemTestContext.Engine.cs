using System.IO.Abstractions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Engines.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Public.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests;

internal partial class ClusterSystemTestContext
{
    private const int EngineCycleTime = 100;

    private readonly Dictionary<(Guid HostId, Guid EngineId), (Guid DeploymentId, Workflow Workflow)> _deploymentWorkflow = [];

    internal void Given_user_added_engineHost(Guid id, bool elevatedPrivileges = false)
    {
        var engineHost = _clusterBuilder.Editors.Application.AddEngineHost(_defaultClusterApplication, id: id, logLevel: ViciOne.Cluster.Builder.EngineHostDefaults.LogLevel);
        engineHost.ElevatedPrivileges = elevatedPrivileges;
    }

    internal void Given_user_added_engine(Guid id, Guid hostId, uint runIndex = 0, uint cycleTime = EngineCycleTime)
    {
        var host = _clusterBuilder.Cache.EngineHosts.Single(x => x.Id == hostId);
        var engine = _clusterBuilder.Editors.EngineHost.AddEngine(host, id.ToString(), id, ViciOne.Cluster.Model.EngineType.DotNet);
        _clusterBuilder.Editors.Engine.SetLogLevel(engine, ViciOne.Cluster.Builder.EngineDefaults.LogLevel);
        _clusterBuilder.Editors.Engine.SetRunIndex(engine, runIndex);
        _clusterBuilder.Editors.Engine.SetMinCycleTime(engine, cycleTime);
    }

    internal void Given_user_disabled_engine(Guid engineId)
    {
        var engine = _clusterBuilder.Cache.EngineIds[engineId];
        _clusterBuilder.Editors.Engine.SetEnabled(engine, false);
    }

    internal async Task Given_engineHost_is_known(Guid id, bool elevatedPrivileges = false)
    {
        using var scope = GetEngineServices(out _, out var database);
        database.EngineHosts.Add(new()
        {
            Id = id,
            ElevatedPrivileges = elevatedPrivileges,
        });
        await database.SaveChangesAsync();
    }

    internal async Task Given_engineHost_is_deployed(Guid id, LogLevel? logLevel = null)
    {
        using var scope = Services.CreateScope();

        var communication = scope.ServiceProvider.GetRequiredService<IEngineHostPoolCommunication>();
        await communication.DeployHost(id, CancellationToken.None);

        var brokerConnectionCache = scope.ServiceProvider.GetRequiredService<IMqttBrokerConnectionCache>();
        var brokerConnection = await brokerConnectionCache.GetMqttConnection(CancellationToken.None);
        var workspaceProvider = scope.ServiceProvider.GetRequiredService<IWorkspaceProvider<ClusterManagementBackendModule>>();
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        var dependenciesDirectory = fileSystem.Path.GetFullPath(fileSystem.GetDependenciesPath(workspaceProvider));

        await communication.SetHostAppSettings(
            id,
            EngineHostAppSettingsFactory.Create(id, id.ToString(), brokerConnection, logLevel ?? ViciOne.Cluster.Builder.EngineHostDefaults.LogLevel,
                dependenciesDirectory),
            CancellationToken.None);
    }

    internal async Task Given_engineHost_is_running(Guid id, bool elevatedPrivileges = false)
    {
        using var scope = Services.CreateScope();
        var communication = scope.ServiceProvider.GetRequiredService<IEngineHostPoolCommunication>();
        await communication.StartHost(id, elevatedPrivileges, CancellationToken.None);
    }

    internal async Task Given_engineHost_is_not_running(Guid id)
    {
        using var scope = Services.CreateScope();
        var communication = scope.ServiceProvider.GetRequiredService<IEngineHostPoolCommunication>();
        await communication.StopHost(id, CancellationToken.None);
    }

    internal async Task Given_engine_is_known(Guid id, Guid hostId, int chainIndex = 0, uint cycleTime = EngineCycleTime, bool shouldRun = false)
    {
        Guid deploymentId;
        Workflow workflow;
        if (_deploymentWorkflow.TryGetValue((hostId, id), out var deploymentWorkflow))
        {
            (deploymentId, workflow) = deploymentWorkflow;
        }
        else
        {
            deploymentId = Guid.NewGuid();
            workflow = BuildMinimalWorkflow(id, chainIndex, cycleTime);
            _deploymentWorkflow[(hostId, id)] = (deploymentId, workflow);
        }
        EngineDeployment deployment = new()
        {
            EngineHostId = hostId,
            EngineId = id,
            Id = deploymentId,
            Checksum = new WorkflowComparer().GetHashCode(workflow),
            Replica = 0,
            ShouldRun = shouldRun,
            Version = "undefined",
        };
        using var scope = GetEngineServices(out _, out var database);
        database.EngineDeployments.Add(deployment);
        await database.SaveChangesAsync();
    }

    private Workflow BuildMinimalWorkflow(Guid engineId, int chainIndex, uint cycleTime) => new()
    {
        Engine = new()
        {
            ChainIndex = chainIndex,
            CycleTime = cycleTime,
            Id = engineId,
            LogLevel = ViciOne.Cluster.Builder.EngineDefaults.LogLevel,
            Name = engineId.ToString(),
        },
        PersistenceCommunication = new()
        {
            Address = s_mqttConnection.Address,
            TcpPort = s_mqttConnection.Port,
            Username = s_mqttConnection.Username,
            Password = s_mqttConnection.Password,
            QualityOfService = 1,
            Retain = true,
        },
        RuntimeFunctionsCommunication = new()
        {
            Address = s_mqttConnection.Address,
            TcpPort = s_mqttConnection.Port,
            Username = s_mqttConnection.Username,
            Password = s_mqttConnection.Password,
            QualityOfService = 2,
            Retain = false,
        },
        EngineLogCommunication = new()
        {
            Id = "E3601083-66AE-41FC-AC20-E05B1DD7936E",
            Properties =
            [
                new("Path") { Value = TypedObject.CreateMandatory(_fileSystem.Path.Combine(_fileSystem.Path.GetFullPath(_fileSystem.GetEnginesDirectory(_workspaceProvider)), $"{engineId}.log")), },
            ],
        },
    };

    internal async Task Given_engine_is_deployed(Guid id, Guid hostId, int chainIndex = 0, uint cycleTime = EngineCycleTime)
    {
        using var scope = GetEngineServices(out var communication, out var database);

        if (_deploymentWorkflow.TryGetValue((hostId, id), out var deploymentWorkflow))
        {
            var deploymentIdentifier = await communication.DeployEngine(hostId, deploymentWorkflow.Workflow, CancellationToken.None);
            _deploymentWorkflow[(hostId, id)] = (deploymentIdentifier, deploymentWorkflow.Workflow);

            var deployment = await GetEngineDeployment(database, hostId, id);
            if (deployment is not null)
            {
                database.EngineDeployments.Remove(deployment);
                deployment = new()
                {
                    EngineHostId = hostId,
                    EngineId = id,
                    Id = deploymentIdentifier,
                    Checksum = deployment.Checksum,
                    Replica = deployment.Replica,
                    ShouldRun = deployment.ShouldRun,
                    Version = deployment.Version,
                };
                database.EngineDeployments.Add(deployment);
                await database.SaveChangesAsync();
            }
        }
        else
        {
            var workflow = BuildMinimalWorkflow(id, chainIndex, cycleTime);
            var deploymentIdentifier = await communication.DeployEngine(hostId, workflow, CancellationToken.None);
            _deploymentWorkflow[(hostId, id)] = (deploymentIdentifier, workflow);
        }
    }

    internal async Task Given_engine_has_functionBlock(Guid id, Guid hostId, Guid functionBlockId)
    {
        var deploymentWorkflow = _deploymentWorkflow[(hostId, id)];
        var containerId = _clusterBuilder.Cluster.Dataflows.First().Root.Id;
        var dummyFunctionBlockAssemblyName = typeof(DummyFunctionBlock).Assembly.GetName();
        deploymentWorkflow.Workflow.ClusterDependencies.Add(new(dummyFunctionBlockAssemblyName.Name!, dummyFunctionBlockAssemblyName.Version!.ToVersionString()));
        deploymentWorkflow.Workflow.Containers.Add(new()
        {
            Id = containerId,
            Name = "Root",
        });
        deploymentWorkflow.Workflow.FunctionBlocks.Add(new()
        {
            ContainerId = containerId,
            CycleFrequency = 1,
            DesignId = DummyFunctionBlock.DesignGuid,
            Id = functionBlockId,
            Name = functionBlockId.ToString(),
        });
        using var scope = GetEngineServices(out _, out var database);
        var deployment = await GetEngineDeployment(database, hostId, id);
        deployment!.Checksum = new WorkflowComparer().GetHashCode(deploymentWorkflow.Workflow);
        await database.SaveChangesAsync();
    }

    internal async Task Given_engine_is_running(Guid id, Guid hostId)
    {
        using var scope = GetEngineServices(out var communication, out _);
        var deploymentWorkflow = _deploymentWorkflow[(hostId, id)];
        await communication.StartEngine(hostId, deploymentWorkflow.DeploymentId, CancellationToken.None);
    }

    internal async Task Then_engineHost_is_known(Guid id)
    {
        using var scope = GetEngineServices(out _, out var database);
        var engineHost = await database.EngineHosts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id);
        engineHost.Should().NotBeNull();
    }

    internal async Task Then_no_engineHost_is_known()
    {
        using var scope = GetEngineServices(out _, out var database);
        (await database.EngineHosts.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    internal void Then_engineHost_is_deployed(Guid id)
    {
        var engineHostDirectory = new DirectoryInfo(Path.Combine(_engineHostsDirectory.Path, id.ToString()));
        engineHostDirectory.Exists.Should().BeTrue();
    }

    internal void Then_no_engineHost_is_deployed()
    {
        var engineHostDirectory = new DirectoryInfo(_engineHostsDirectory.Path);
        engineHostDirectory.EnumerateDirectories().Should().BeEmpty();
    }

    internal void Then_engineHost_is_running(Guid id)
    {
        var processIdFile = new FileInfo(Path.Combine(_engineHostsDirectory.Path, id.ToString(), "processid"));
        processIdFile.Exists.Should().BeTrue();
        var process = GetEngineHostProcess(processIdFile.FullName);
        process.HasExited.Should().BeFalse();
    }

    internal void Then_engineHost_is_not_running(Guid id)
    {
        var processIdFile = new FileInfo(Path.Combine(_engineHostsDirectory.Path, id.ToString(), "processid"));
        processIdFile.Exists.Should().BeFalse();
    }

    internal async Task Then_engine_is_known(Guid id, Guid hostId, bool shouldRun = false)
    {
        using var scope = GetEngineServices(out _, out var database);
        var deployment = await GetEngineDeployment(database, hostId, id);
        deployment.Should().NotBeNull();
        deployment!.ShouldRun.Should().Be(shouldRun);
    }

    internal async Task Then_no_engine_is_known()
    {
        using var scope = GetEngineServices(out _, out var database);
        (await database.EngineDeployments.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    internal async Task Then_engine_is_deployed(Guid id, Guid hostId)
    {
        using var scope = GetEngineServices(out _, out var database);
        var deployment = await GetEngineDeployment(database, hostId, id);
        DirectoryInfo deploymentDirectory = new(Path.Combine(_engineHostsDirectory.Path, hostId.ToString(), "deployments", deployment!.Id.ToString()));
        deploymentDirectory.Exists.Should().BeTrue();
        deploymentDirectory.EnumerateFiles().Should().NotBeEmpty();
    }

    internal void Then_no_engine_is_deployed()
        => Directory.GetDirectories(_engineHostsDirectory.Path, "deployments", SearchOption.AllDirectories)
            .SelectMany(Directory.GetDirectories)
            .Should().BeEmpty();

    internal async Task Then_engine_is_running(Guid id, Guid hostId)
    {
        using var scope = GetEngineServices(out var communication, out var database);
        var deployment = await GetEngineDeployment(database, hostId, id);
        var state = await communication.GetEngineState(hostId, deployment!.Id, CancellationToken.None);
        state.Should().Be(EngineState.Running);
    }

    internal async Task Then_no_engine_is_running()
    {
        using var scope = GetEngineServices(out var communication, out _);
        foreach (var engineHostDirectory in Directory.GetDirectories(_engineHostsDirectory.Path))
        {
            var engineHost = Guid.Parse(Path.GetFileName(engineHostDirectory));
            var deploymentsDirectory = Path.Combine(engineHostDirectory, "deployments");
            if (Path.Exists(deploymentsDirectory))
            {
                foreach (var deploymentDirectory in Directory.GetDirectories(deploymentsDirectory))
                {
                    var deploymentId = Guid.Parse(Path.GetFileName(deploymentDirectory));
                    var state = await communication.GetEngineState(engineHost, deploymentId, CancellationToken.None);
                    state.Should().Be(EngineState.Stopped);
                }
            }
        }
    }

    internal void Then_engine_was_started(Guid id)
        => Then(_ => _events.Should().ContainEquivalentOf<EngineStarted>(new(id)));

    internal void Then_no_engine_was_started()
        => Then(_ => _events.Should().NotContainItemsAssignableTo<EngineStarted>());

    internal void Then_engine_was_stopped(Guid id)
        => Then(_ => _events.Should().ContainEquivalentOf<EngineStopped>(new(id)));

    internal void Then_no_engine_was_stopped()
        => Then(_ => _events.Should().NotContainItemsAssignableTo<EngineStopped>());

    internal void Then_engine_was_torn_down(Guid id)
        => Then(_ => _events.Should().ContainEquivalentOf<EngineTornDown>(new(id)));

    internal void Then_no_engine_was_torn_down()
        => Then(_ => _events.Should().NotContainItemsAssignableTo<EngineTornDown>());

    private IServiceScope GetEngineServices(out IEngineHostCommunication communication, out IEngineDbContext database)
    {
        var scope = Services.CreateScope();
        database = scope.ServiceProvider.GetRequiredService<IEngineDbContext>();
        communication = scope.ServiceProvider.GetRequiredService<IEngineHostCommunication>();
        return scope;
    }

    private static Task<EngineDeployment?> GetEngineDeployment(IEngineDbContext database, Guid engineHostUniqueId, Guid engineUniqueId)
        => database.EngineDeployments.SingleOrDefaultAsync(x => x.EngineHostId == engineHostUniqueId && x.EngineId == engineUniqueId);
}
