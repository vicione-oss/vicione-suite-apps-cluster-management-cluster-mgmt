using System.ComponentModel;
using System.IO.Abstractions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Engines.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Connections.Contracts;
using Sdk.Instance;
using Sdk.Messaging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using Engine = ViciOne.Cluster.Model.Engine;
using EngineHost = ViciOne.Cluster.Model.EngineHost;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

[ReadOnlyConsumer]
public sealed class UpdateDeploymentsConsumer(IInstanceInformationProvider instanceInfoProvider,
    IEngineDbContext engineDbContext, IEngineHostPoolCommunication poolCommunication, IEngineHostCommunication hostCommunication,
    IMqttBrokerConnectionCache brokerConnectionCache, IDesignProvider designProvider, IClusterDbContext clusterContext,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider, ISuiteRulesetProvider rulesetProvider,
    IFileSystem fileSystem, ILogger<UpdateDeploymentsConsumer> logger) : IConsumer<UpdateDeployments>
{
    private readonly IInstanceInformationProvider _instanceInfoProvider = instanceInfoProvider;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;
    private readonly IEngineHostPoolCommunication _poolCommunication = poolCommunication;
    private readonly IEngineHostCommunication _hostCommunication = hostCommunication;
    private readonly IMqttBrokerConnectionCache _brokerConnectionCache = brokerConnectionCache;
    private readonly IDesignProvider _designProvider = designProvider;
    private readonly IClusterDbContext _clusterContext = clusterContext;
    private readonly ISuiteRulesetProvider _rulesetProvider = rulesetProvider;
    private readonly ILogger<UpdateDeploymentsConsumer> _logger = logger;
    private readonly string _packagesDirectory = fileSystem.Path.GetFullPath(fileSystem.GetDependenciesPath(workspaceProvider));
    private readonly string _engineLogDirectory = fileSystem.Path.GetFullPath(fileSystem.GetEnginesDirectory(workspaceProvider));

    /// <summary>
    /// if something bad happens 3 retry attempts get executed. we have to ensure to cleanup on exceptions to have a fresh attempt and also
    /// handle the Fault-Consume in case the retry attempts failed.
    /// </summary>
    /// <param name="context"></param>
    public async Task Consume(ConsumeContext<UpdateDeployments> context)
    {
        var clusterApplicationId = _instanceInfoProvider.Local.Id;
        try
        {
            var targetCluster = await ClusterSerializer.Decompress(context.Message.CompressedTargetCluster);

            _logger.LogInformation("UpdateDeployments on instance:{ApplicationId} for cluster {ClusterId} v{Version}",
                clusterApplicationId, targetCluster.Id, targetCluster.Version);

            var dependencyResolver = await _designProvider.CreateResolver();
            using var builder = new ClusterBuilder(targetCluster, dependencyResolver);

            MqttConnection brokerConnection;
            try
            {
                brokerConnection = await _brokerConnectionCache.GetMqttConnection(context.CancellationToken);
                _logger.LogInformation("Use broker {Protocol} connection address {Name}:{Port} user:{User}",
                    brokerConnection.Protocol, brokerConnection.Address, brokerConnection.Port, brokerConnection.Username);
            }
            catch (Exception e)
            {
                throw ClusterErrorInfos.MqttConnectionMisconfigured().AsException(e);
            }

            // assumes that the database knows the local instance
            var clusterApplication = builder.Cache.ClusterApplications.SingleOrDefault(app => app.Id == clusterApplicationId);

            var desiredEngineHosts = clusterApplication?.EngineHosts ?? [];

            LogRelevanceInformation(builder, desiredEngineHosts);

            var expectedEngineHosts = await _engineDbContext.EngineHosts.Include(eh => eh.Deployments).ToListAsync(context.CancellationToken);
            var physicalEngineHosts = await _poolCommunication.GetStates(context.CancellationToken);
            LogDeployedInformation(expectedEngineHosts);

            var enginesDataPorts = TransformDataPorts(builder, desiredEngineHosts);
            builder.Editors.Cluster.SortFunctionBlocks();

            var engineHostActions = IdentifyEngineHostActions(desiredEngineHosts, expectedEngineHosts, physicalEngineHosts, targetCluster,
                dependencyResolver, brokerConnection, enginesDataPorts);
            engineHostActions = OptimizeEngineHostActions(engineHostActions);
            await UpdateEngineHosts(engineHostActions, context, context.CancellationToken);

            if (_instanceInfoProvider.Local.Type == InstanceType.Slave)
            {
                await _clusterContext.UpsertActiveClusterInfo(targetCluster.Id, targetCluster.Version, DateTimeOffset.UtcNow, null, context.CancellationToken);
            }

            await context.Publish(
                new ClusterNodeDeploymentsUpdatedEvent(context.CorrelationId!.Value, clusterApplicationId),
                context.CancellationToken);
        }
        catch (ClusterUpdateException e)
        {
            _logger.LogError(e, "Error deploying on application {ClusterApplicationId}", clusterApplicationId);
            var errorInfo = new ErrorInfo(e.ErrorCode, e.InnerException is null ? e.Message : e.InnerException.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.CorrelationId!.Value,
                    clusterApplicationId,
                    errorInfo),
                context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            var errorInfo = ClusterErrorInfos.DeploymentCancelled(clusterApplicationId);
            _logger.LogError("{Message}", errorInfo.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.CorrelationId!.Value,
                    clusterApplicationId,
                    errorInfo),
                context.CancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error deploying on application {ClusterApplicationId}", clusterApplicationId);
            var errorInfo = new ErrorInfo(-1, e.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.CorrelationId!.Value, clusterApplicationId, errorInfo),
                context.CancellationToken);
        }
    }

    private IDictionary<Engine, List<DataPortTransformation>> TransformDataPorts(
        ClusterBuilder builder, IReadOnlyCollection<EngineHost> engineHosts)
    {
        try
        {
            return DataPortTransformer.TransformToFunctionBlocks(builder, engineHosts, _rulesetProvider);
        }
        catch (Exception e)
        {
            throw ClusterErrorInfos.DataPortTransformationFailed().AsException(e);
        }
    }

    private List<IEngineHostAction> IdentifyEngineHostActions(
        IReadOnlyCollection<EngineHost> desiredEngineHosts,
        IReadOnlyCollection<EngineHosts.EngineHost> expectedEngineHosts,
        IReadOnlyDictionary<Guid, EngineHostState> physicalEngineHosts,
        Cluster cluster,
        IDependencyResolver dependencyResolver,
        MqttConnection brokerConnection,
        IDictionary<Engine, List<DataPortTransformation>> enginesDataPorts)
    {
        List<IEngineHostAction> actions = [];
        var physicalStates = physicalEngineHosts.ToDictionary();
        foreach (var desiredEngineHost in desiredEngineHosts)
        {
            physicalStates.Remove(desiredEngineHost.Id, out var actualState);
            var appSettings = EngineHostAppSettingsFactory.Create(desiredEngineHost.Id, desiredEngineHost.Name, brokerConnection, desiredEngineHost.LogLevel, _packagesDirectory);
            var expectedEngineHost = expectedEngineHosts.SingleOrDefault(e => e.Id == desiredEngineHost.Id);
            EngineHostProvisioning hostProvisioning = new(desiredEngineHost, appSettings, expectedEngineHost, actualState);
            actions.Add(hostProvisioning);

            foreach (var desiredEngine in desiredEngineHost.Engines)
            {
                Workflow workflow;
                try
                {
                    workflow = ClusterToWorkflowTranslator.CreateWorkflow(cluster, desiredEngineHost, desiredEngine, dependencyResolver, brokerConnection, enginesDataPorts, _engineLogDirectory);
                }
                catch (Exception e)
                {
                    throw ClusterErrorInfos.CouldNotCreateDeployParameter().AsException(e);
                }
                var expectedEngineDeployment = expectedEngineHost?.Deployments.SingleOrDefault(e => e.EngineId == desiredEngine.Id);
                hostProvisioning.EngineActions.Add(new EngineProvisioning(desiredEngine, workflow, expectedEngineDeployment));
            }

            foreach (var expectedEngineDeployment in expectedEngineHost?.Deployments.Where(e => !desiredEngineHost.Engines.Select(x => x.Id).Contains(e.EngineId)) ?? [])
                hostProvisioning.EngineActions.Add(new EngineDisposal(expectedEngineDeployment));
        }

        foreach (var engineHost in expectedEngineHosts.Where(eh => !desiredEngineHosts.Select(x => x.Id).Contains(eh.Id)))
        {
            physicalStates.Remove(engineHost.Id, out var actualState);
            actions.Add(new EngineHostDisposal(engineHost, actualState)
            {
                EngineActions = [.. engineHost.Deployments.Select(e => new EngineDisposal(e))],
            });
        }
        foreach (var actualState in physicalStates)
            actions.Add(new EngineHostDisposal(new EngineHosts.EngineHost() { Id = actualState.Key, }, actualState.Value));
        return actions;
    }

    private interface IEngineHostAction;
    private sealed record EngineHostProvisioning(
        EngineHost Desired, EngineHostAppSettings AppSettings, EngineHosts.EngineHost? Expected, EngineHostState PhysicalState) : IEngineHostAction
    {
        public List<IEngineAction> EngineActions { get; init; } = [];
    }
    private sealed record EngineHostDisposal(EngineHosts.EngineHost Expected, EngineHostState PhysicalState) : IEngineHostAction
    {
        public List<EngineDisposal> EngineActions { get; init; } = [];
    }
    private interface IEngineAction;
    private sealed record EngineProvisioning(Engine Desired, Workflow Workflow, EngineDeployment? Expected = default) : IEngineAction;
    private sealed record EngineDisposal(EngineDeployment Expected) : IEngineAction;

    private static List<IEngineHostAction> OptimizeEngineHostActions(List<IEngineHostAction> engineHostActions)
    {
        List<IEngineHostAction> optimizedActions = [];
        foreach (var engineHostAction in engineHostActions)
        {
            if (engineHostAction is EngineHostProvisioning engineHostProvisioning)
            {
                // skip provisioning of engine host if no engines will be inside
                if (engineHostProvisioning.Expected is null && engineHostProvisioning.EngineActions.Count == 0 && engineHostProvisioning.PhysicalState == EngineHostState.NotFound)
                    continue;

                // dispose engine host if no engines will be inside
                if ((engineHostProvisioning.Expected is not null || engineHostProvisioning.PhysicalState != EngineHostState.NotFound) &&
                    (engineHostProvisioning.EngineActions.Count == 0 || engineHostProvisioning.EngineActions.TrueForAll(a => a is EngineDisposal)))
                {
                    optimizedActions.Add(new EngineHostDisposal(new EngineHosts.EngineHost() { Id = engineHostProvisioning.Desired.Id, }, engineHostProvisioning.PhysicalState)
                    {
                        EngineActions = [.. engineHostProvisioning.EngineActions.OfType<EngineDisposal>(),],
                    });
                }
                else
                {
                    optimizedActions.Add(engineHostAction);
                }
            }
            else
            {
                optimizedActions.Add(engineHostAction);
            }
        }
        return optimizedActions;
    }

    private async Task UpdateEngineHosts(IReadOnlyCollection<IEngineHostAction> engineHostActions, ConsumeContext context,
        CancellationToken cancellationToken)
    {
        foreach (var engineHostAction in engineHostActions)
        {
            switch (engineHostAction)
            {
                case EngineHostProvisioning provisioning: await ProvisionEngineHost(provisioning, context, cancellationToken); break;
                case EngineHostDisposal disposal: await DisposeEngineHost(disposal, context, cancellationToken); break;
            }
        }
    }

    private async Task ProvisionEngineHost(EngineHostProvisioning engineHostProvisioning, ConsumeContext context, CancellationToken cancellationToken)
    {
        await ProvisionEngineHost(engineHostProvisioning, cancellationToken);
        var deploymentStates = await GetDeploymentStates(engineHostProvisioning.Desired.Id, EngineHostState.Started, cancellationToken);
        var deploymentActions = IdentifyDeploymentActions(engineHostProvisioning, deploymentStates);
        await UpdateDeployments(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.Name, context, deploymentActions, cancellationToken);
    }

    private async Task ProvisionEngineHost(EngineHostProvisioning engineHostProvisioning, CancellationToken cancellationToken)
    {
        switch (engineHostProvisioning.PhysicalState)
        {
            case EngineHostState.NotFound:
                _logger.LogDeployingEngineHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.Name);
                await _poolCommunication.DeployHost(engineHostProvisioning.Desired.Id, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotDeployEngineHost(), cancellationToken);
                await _poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                _logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                await _poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                break;
            case EngineHostState.Started:
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                var appSettingsChanged = await _poolCommunication.CompareHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotGetAppSettingsOfEngineHost(), cancellationToken) != 0;
                var elevatedPrivilegesChanged = false;
                if (engineHostProvisioning.Expected is not null)
                    elevatedPrivilegesChanged = engineHostProvisioning.Expected.ElevatedPrivileges != engineHostProvisioning.Desired.ElevatedPrivileges;
                if (appSettingsChanged || elevatedPrivilegesChanged)
                {
                    _logger.LogStoppingEngineHost(engineHostProvisioning.Desired.Id);
                    await _poolCommunication.StopHost(engineHostProvisioning.Desired.Id, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotStopEngineHost(), cancellationToken);

                    if (appSettingsChanged)
                    {
                        _logger.LogUpdatingEngineHost(engineHostProvisioning.Desired.Id);
                        await _poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                            .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                    }
                    if (elevatedPrivilegesChanged && engineHostProvisioning.Expected is not null)
                    {
                        engineHostProvisioning.Expected.ElevatedPrivileges = engineHostProvisioning.Desired.ElevatedPrivileges;
                        await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);
                    }

                    _logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                    await _poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                }
                else
                {
                    _logger.LogSkipEngineHostUpdate(engineHostProvisioning.Desired.Id);
                }
                break;
            case EngineHostState.Stopped:
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                if (await _poolCommunication.CompareHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotGetAppSettingsOfEngineHost(), cancellationToken) != 0)
                {
                    _logger.LogUpdatingEngineHost(engineHostProvisioning.Desired.Id);
                    await _poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                }
                else
                {
                    _logger.LogSkipEngineHostUpdate(engineHostProvisioning.Desired.Id);
                }
                _logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                await _poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(engineHostProvisioning.PhysicalState), (int)engineHostProvisioning.PhysicalState, typeof(EngineHostState));
        }

        async Task EnsureEngineHostInDatabase(EngineHostProvisioning ehProvisioning, CancellationToken cancelToken)
        {
            if (ehProvisioning.Expected is null)
            {
                _engineDbContext.EngineHosts.Add(new()
                {
                    Id = ehProvisioning.Desired.Id,
                    ElevatedPrivileges = ehProvisioning.Desired.ElevatedPrivileges,
                });
                await _engineDbContext.Instance.SaveChangesAsync(cancelToken);
            }
        }
    }

    private DeploymentActions IdentifyDeploymentActions(EngineHostProvisioning engineHostProvisioning,
        Dictionary<Guid, EngineState> physicalDeploymentStates)
    {
        DeploymentActions actions = new();

        WorkflowComparer workflowComparer = new();
        foreach (var engineAction in engineHostProvisioning.EngineActions)
        {
            if (engineAction is EngineProvisioning engineProvisioning)
            {
                var desired = engineProvisioning.Desired;
                var expected = engineProvisioning.Expected;
                if (expected is not null)
                {
                    if (physicalDeploymentStates.Remove(expected.Id, out var actualState))
                    {
                        var raiseStop = actualState == EngineState.Running
                            || (!desired.Enabled && expected.ShouldRun && actualState == EngineState.Stopped)
                            || (!desired.Enabled && !expected.ShouldRun && engineHostProvisioning.PhysicalState == EngineHostState.Stopped && actualState == EngineState.Stopped);
                        if (workflowComparer.GetHashCode(engineProvisioning.Workflow) == expected.Checksum)
                        {
                            if (desired.Enabled && !expected.ShouldRun)
                                actions.ShouldRun.Add(expected);
                            else if (!desired.Enabled && expected.ShouldRun)
                                actions.ShouldNotRun.Add(expected);

                            if (desired.Enabled && actualState == EngineState.Running)
                            {
                                _logger.LogSkipEngineUpdate(desired.Id, expected.EngineHostId, expected.Id);
                            }
                            else
                            {
                                if (desired.Enabled && actualState == EngineState.Stopped)
                                    actions.Start.Add(expected);
                                else if (!desired.Enabled)
                                    actions.Stop.Add((expected.EngineHostId, expected.Id, expected.EngineId, actualState == EngineState.Running, raiseStop));
                            }
                        }
                        else
                        {
                            _logger.LogUpdatingEngine(desired.Id, expected.EngineHostId, expected.Id);
                            actions.ShouldNotRun.Add(expected);
                            actions.Stop.Add((expected.EngineHostId, expected.Id, expected.EngineId, actualState == EngineState.Running, raiseStop));
                            actions.Delete.Add(expected);
                            actions.TearDown.Add((expected.EngineHostId, expected.Id, expected.EngineId, true, true));
                            actions.Deploy.Add((desired, engineProvisioning.Workflow));
                        }
                    }
                    else
                    {
                        _logger.LogRestoreMissingEngine(desired.Id, expected.EngineHostId, expected.Id);
                        actions.Delete.Add(expected);
                        actions.Deploy.Add((desired, engineProvisioning.Workflow));
                    }
                }
                else
                {
                    actions.Deploy.Add((desired, engineProvisioning.Workflow));
                }
            }
            else if (engineAction is EngineDisposal engineDisposal)
            {
                var expected = engineDisposal.Expected;
                if (physicalDeploymentStates.Remove(expected.Id, out var actualState))
                {
                    if (actualState == EngineState.Running)
                    {
                        actions.ShouldNotRun.Add(expected);
                        actions.Stop.Add((expected.EngineHostId, expected.Id, expected.EngineId, true, true));
                    }
                }
                actions.Delete.Add(expected);
                actions.TearDown.Add((expected.EngineHostId, expected.Id, expected.EngineId, true, true));
            }
        }
        foreach (var actualState in physicalDeploymentStates)
        {
            _logger.LogRemoveUnexpectedEngine(engineHostProvisioning.Desired.Id, actualState.Key);
            EngineDeployment unexpectedEngineDeployment = new() { Id = actualState.Key, EngineHostId = engineHostProvisioning.Desired.Id, };
            if (actualState.Value == EngineState.Running)
                actions.Stop.Add((unexpectedEngineDeployment.EngineHostId, unexpectedEngineDeployment.Id, Guid.Empty, true, false));
            actions.TearDown.Add((unexpectedEngineDeployment.EngineHostId, unexpectedEngineDeployment.Id, Guid.Empty, true, false));
        }
        return actions;
    }

    private async Task DisposeEngineHost(EngineHostDisposal engineHostDisposal, ConsumeContext context, CancellationToken cancellationToken)
    {
        var deploymentStates = await GetDeploymentStates(engineHostDisposal.Expected.Id, engineHostDisposal.PhysicalState, cancellationToken);
        var deploymentActions = IdentifyDeploymentActions(engineHostDisposal, deploymentStates);
        await UpdateDeployments(engineHostDisposal.Expected.Id, "<currently unavailable>", context, deploymentActions, cancellationToken);
        await DisposeEngineHost(engineHostDisposal, cancellationToken);
    }

    private static DeploymentActions IdentifyDeploymentActions(EngineHostDisposal engineHostDisposal, Dictionary<Guid, EngineState> physicalDeploymentStates)
    {
        DeploymentActions actions = new();

        if (engineHostDisposal.EngineActions.Count > 0)
        {
            foreach (var engineDisposal in engineHostDisposal.EngineActions)
            {
                var execute = false;
                var raiseEvent = engineDisposal.Expected.ShouldRun;
                if (physicalDeploymentStates.TryGetValue(engineDisposal.Expected.Id, out var engineState))
                {
                    if (engineState == EngineState.Running)
                    {
                        execute = true;
                        raiseEvent = true;
                    }
                }
                else
                {
                    raiseEvent = true;
                }
                actions.Stop.Add((engineDisposal.Expected.EngineHostId, engineDisposal.Expected.Id, engineDisposal.Expected.EngineId, execute, raiseEvent));
            }

            actions.Delete.AddRange(engineHostDisposal.EngineActions.Select(e => e.Expected));

            foreach (var engineDisposal in engineHostDisposal.EngineActions)
            {
                var execute = false;
                if (physicalDeploymentStates.Remove(engineDisposal.Expected.Id, out var _))
                    execute = true;
                actions.TearDown.Add((engineDisposal.Expected.EngineHostId, engineDisposal.Expected.Id, engineDisposal.Expected.EngineId, execute, true));
            }
        }

        foreach (var deploymentState in physicalDeploymentStates)
        {
            if (deploymentState.Value == EngineState.Running)
                actions.Stop.Add((engineHostDisposal.Expected.Id, deploymentState.Key, Guid.Empty, true, false));
            actions.TearDown.Add((engineHostDisposal.Expected.Id, deploymentState.Key, Guid.Empty, true, false));
        }

        return actions;
    }

    private async Task DisposeEngineHost(EngineHostDisposal engineHostDisposal, CancellationToken cancellationToken)
    {
        if (engineHostDisposal.PhysicalState == EngineHostState.Started)
        {
            _logger.LogStoppingEngineHost(engineHostDisposal.Expected.Id);
            await _poolCommunication.StopHost(engineHostDisposal.Expected.Id, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotStopEngineHost(), cancellationToken);
        }

        if (engineHostDisposal.PhysicalState != EngineHostState.NotFound)
        {
            _logger.LogTearingDownEngineHost(engineHostDisposal.Expected.Id);
            await _poolCommunication.TearDownHost(engineHostDisposal.Expected.Id, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotTearDownEngineHost(), cancellationToken);
        }

        var dbEngineHost = await _engineDbContext.EngineHosts.SingleOrDefaultAsync(_ => _.Id == engineHostDisposal.Expected.Id, cancellationToken);
        if (dbEngineHost is not null)
        {
            _engineDbContext.EngineHosts.Remove(dbEngineHost);
            await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, EngineState>> GetDeploymentStates(Guid engineHostId, EngineHostState physicalState,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, EngineState> deploymentStates = [];
        if (physicalState == EngineHostState.Started)
        {
            deploymentStates = new(await _hostCommunication.GetDeploymentStates(engineHostId, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotGetRunStatesOfEngines(), cancellationToken));
        }
        return deploymentStates;
    }

    private async Task UpdateDeployments(Guid engineHostId, string engineHostName, ConsumeContext context, DeploymentActions actions,
        CancellationToken cancellationToken)
    {
        if (actions.ShouldNotRun.Count != 0)
        {
            foreach (var deployment in actions.ShouldNotRun)
                deployment.ShouldRun = false;
            await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);
        }

        if (actions.Stop.Count != 0)
        {
            foreach (var stop in actions.Stop)
            {
                _logger.LogStoppingEngine(stop.EngineId, stop.EngineHostId, stop.DeploymentId);
                var stopped = !stop.Execute && stop.RaiseEvent;
                if (stop.Execute && await _hostCommunication.StopEngine(stop.EngineHostId, stop.DeploymentId, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStopEngine(), cancellationToken))
                {
                    stopped = true;
                }
                if (stop.RaiseEvent && stopped)
                    await context.Publish(new EngineStopped(stop.EngineId), cancellationToken);
            }
        }

        if (actions.Delete.Count != 0)
        {
            foreach (var deployment in actions.Delete)
                _engineDbContext.EngineDeployments.Remove(deployment);
            await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);
        }

        if (actions.TearDown.Count != 0)
        {
            foreach (var tearDown in actions.TearDown)
            {
                _logger.LogTearingDownEngine(tearDown.EngineId, tearDown.EngineHostId, tearDown.DeploymentId);
                var tornDown = !tearDown.Execute && tearDown.RaiseEvent;
                if (tearDown.Execute)
                {
                    await _hostCommunication.TearDownEngine(tearDown.EngineHostId, tearDown.DeploymentId, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotTearDownEngine(), cancellationToken);
                    tornDown = true;
                }
                if (tearDown.RaiseEvent && tornDown)
                    await context.Publish(new EngineTornDown(tearDown.EngineId), context.CancellationToken);
            }
        }

        foreach (var deployment in actions.Deploy)
        {
            _logger.LogDeployingEngine(deployment.Engine.Id, deployment.Engine.Name, engineHostId, engineHostName);
            var deploymentIdentifier = await _hostCommunication.DeployEngine(engineHostId, deployment.Workflow, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotDeployEngine(), cancellationToken);

            EngineDeployment engineDeployment = new()
            {
                Id = deploymentIdentifier,
                EngineHostId = engineHostId,
                EngineId = deployment.Engine.Id,
                Checksum = new WorkflowComparer().GetHashCode(deployment.Workflow),
            };
            _engineDbContext.EngineDeployments.Add(engineDeployment);
            await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);

            if (deployment.Engine.Enabled)
            {
                actions.ShouldRun.Add(engineDeployment);
                actions.Start.Add(engineDeployment);
            }
        }

        if (actions.ShouldRun.Count != 0)
        {
            foreach (var deployment in actions.ShouldRun)
                deployment.ShouldRun = true;
            await _engineDbContext.Instance.SaveChangesAsync(cancellationToken);
        }

        if (actions.Start.Count != 0)
        {
            foreach (var deployment in actions.Start)
            {
                _logger.LogStartingEngine(deployment.EngineId, deployment.EngineHostId, deployment.Id);
                if (await _hostCommunication.StartEngine(deployment.EngineHostId, deployment.Id, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngine(), cancellationToken))
                {
                    await context.Publish(new EngineStarted(deployment.EngineId), cancellationToken);
                }
            }
        }
    }

    private sealed class DeploymentActions
    {
        public List<EngineDeployment> ShouldNotRun { get; init; } = [];
        public List<(Guid EngineHostId, Guid DeploymentId, Guid EngineId, bool Execute, bool RaiseEvent)> Stop { get; init; } = [];
        public List<EngineDeployment> Delete { get; init; } = [];
        public List<(Guid EngineHostId, Guid DeploymentId, Guid EngineId, bool Execute, bool RaiseEvent)> TearDown { get; init; } = [];
        public List<(Engine Engine, Workflow Workflow)> Deploy { get; init; } = [];
        public List<EngineDeployment> ShouldRun { get; init; } = [];
        public List<EngineDeployment> Start { get; init; } = [];
    }

    private void LogDeployedInformation(IEnumerable<EngineHosts.EngineHost> hosts)
        => LogGuids(
            [.. hosts.Select(k => k.Id)],
            [.. hosts.SelectMany(k => k.Deployments).Select(k => k.EngineId)],
            null,
            "Deployed");

    private void LogRelevanceInformation(ClusterBuilder builder, IReadOnlyCollection<EngineHost> hosts)
    {
        var hostedEngines = hosts.SelectMany(h => h.Engines);
        var allDataPorts = hostedEngines.SelectMany(k => builder.Cache.EngineDataPorts.TryGetValue(k, out var dataPorts) ? dataPorts : []);
        LogGuids(
            [.. hosts.Select(k => k.Id)],
            [.. hostedEngines.Select(k => k.Id)],
            [.. allDataPorts.Select(k => k.Id)],
            "Relevant");
    }

    private void LogGuids(IReadOnlyCollection<Guid> hostsIds, IReadOnlyCollection<Guid> engineIds,
        IReadOnlyCollection<Guid>? dataPortIds, string step)
    {
        if (hostsIds.Count == 0 && engineIds.Count == 0)
            return;

        _logger.LogInformation("{Step} engine hosts:{HostIds} engines:{EngineIds}{DataPortInfo}",
            step,
            string.Join(", ", hostsIds),
            string.Join(", ", engineIds),
            dataPortIds == null ? string.Empty : $" with data ports:{string.Join(", ", dataPortIds)}");
    }
}
