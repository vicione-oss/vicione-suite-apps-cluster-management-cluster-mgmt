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
using MQTTnet;
using Sdk.Backend.Modules;
using Sdk.Connections.Contracts;
using Sdk.Instance;
using Sdk.Messaging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;
using Engine = ViciOne.Cluster.Model.Engine;
using EngineHost = ViciOne.Cluster.Model.EngineHost;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

[ReadOnlyConsumer]
public sealed class UpdateDeploymentsConsumer(IInstanceInformationProvider instanceInfoProvider,
    IEngineDbContext engineDbContext,
    IEngineHostPoolCommunication poolCommunication,
    IEngineHostCommunication hostCommunication,
    IMqttBrokerConnectionCache brokerConnectionCache,
    IDesignProvider designProvider,
    IClusterDbContext clusterContext,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    ISuiteRulesetProvider rulesetProvider,
    IFileSystem fileSystem,
    ILogger<UpdateDeploymentsConsumer> logger,
    IMqttClient? mqttClientSubstitute = default) : IConsumer<UpdateDeployments>
{
    private readonly string _packagesDirectory = fileSystem.Path.GetFullPath(fileSystem.GetDependenciesPath(workspaceProvider));
    private readonly string _engineLogDirectory = fileSystem.Path.GetFullPath(fileSystem.GetEnginesDirectory(workspaceProvider));

    /// <summary>
    /// if something bad happens 3 retry attempts get executed. we have to ensure to clean up on exceptions to have a fresh attempt and also
    /// handle the Fault-Consume in case the retry attempts failed.
    /// </summary>
    /// <param name="context"></param>
    public async Task Consume(ConsumeContext<UpdateDeployments> context)
    {
        var clusterApplicationId = instanceInfoProvider.Local.Id;
        IMqttClient? mqttClient = default;
        try
        {
            var targetCluster = await ClusterSerializer.Decompress(context.Message.CompressedTargetCluster);

            logger.LogInformation("UpdateDeployments on instance:{ApplicationId} for cluster {ClusterId} v{Version}",
                clusterApplicationId, targetCluster.Id, targetCluster.Version);

            var dependencyResolver = await designProvider.CreateResolver();
            using var builder = new ClusterBuilder(targetCluster, dependencyResolver);

            MqttConnection brokerConnection;
            try
            {
                brokerConnection = await brokerConnectionCache.GetMqttConnection(context.CancellationToken);
                logger.LogInformation("Use broker {Protocol} connection address {Name}:{Port} user:{User}",
                    brokerConnection.Protocol, brokerConnection.Address, brokerConnection.Port, brokerConnection.Username);
            }
            catch (Exception e)
            {
                throw ClusterErrorInfos.MqttConnectionMisconfigured().AsException(e);
            }

            // assumes that the database knows the local instance
            var clusterApplication = builder.Cache.ClusterApplications.SingleOrDefault(app => app.Id == clusterApplicationId);

            var desiredEngineHosts = clusterApplication?.EngineHosts ?? [];
            LogDeploymentVolume(desiredEngineHosts, builder.Cache);

            (mqttClient, var expectedEngineHosts, var physicalEngineHosts) = await WhenAll(
                brokerConnectionCache.ConnectClient(mqttClientSubstitute, default, context.CancellationToken), //this default should probably be `mqttClient` instead
                engineDbContext.EngineHosts.Include(eh => eh.Deployments).ToListAsync(context.CancellationToken),
                poolCommunication.GetStates(context.CancellationToken));
            var expectedRetainMessages = GetExpectedRetainMessages(expectedEngineHosts);

            var enginesDataPorts = TransformDataPorts(builder, desiredEngineHosts);
            builder.Editors.Cluster.SortFunctionBlocks();

            var engineHostActions = IdentifyEngineHostActions(desiredEngineHosts, expectedEngineHosts, physicalEngineHosts, targetCluster,
                dependencyResolver, brokerConnection, enginesDataPorts);
            engineHostActions = OptimizeEngineHostActions(engineHostActions);
            await UpdateEngineHosts(engineHostActions, context, context.CancellationToken);

            await DeleteRetainMessages(mqttClient, expectedRetainMessages, context.CancellationToken);

            if (instanceInfoProvider.Local.Type == InstanceType.Slave)
            {
                await clusterContext.UpsertActiveClusterInfo(targetCluster.Id, targetCluster.Version, DateTimeOffset.UtcNow, null, context.CancellationToken);
            }

            await context.Publish(
                new ClusterNodeDeploymentsUpdatedEvent(context.Message.CorrelationId, context.Message.ClusterId, clusterApplicationId),
                context.CancellationToken);
        }
        catch (ClusterUpdateException e)
        {
            logger.LogError(e, "Error deploying on application {ClusterApplicationId}", clusterApplicationId);
            var errorInfo = new ErrorInfo(e.ErrorCode, e.InnerException is null ? e.Message : e.InnerException.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.Message.CorrelationId,
                    context.Message.ClusterId,
                    clusterApplicationId,
                    errorInfo),
                context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            var errorInfo = ClusterErrorInfos.DeploymentCancelled(clusterApplicationId);
            logger.LogError("{Message}", errorInfo.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.Message.CorrelationId,
                    context.Message.ClusterId,
                    clusterApplicationId,
                    errorInfo),
                context.CancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error deploying on application {ClusterApplicationId}", clusterApplicationId);
            var errorInfo = new ErrorInfo(-1, e.Message);
            await context.Publish(
                new ClusterNodeDeploymentsUpdateFailedEvent(context.Message.CorrelationId,
                    context.Message.ClusterId,
                    clusterApplicationId,
                    errorInfo),
                context.CancellationToken);
        }
        finally
        {
            if (mqttClient is not null)
                await mqttClient.DisconnectClient(mqttClientSubstitute, default, context.CancellationToken);
        }
    }

    private static async Task<(A, B, C)> WhenAll<A, B, C>(Task<A> taskA, Task<B> taskB, Task<C> taskC)
    {
        await Task.WhenAll(taskA, taskB, taskC);
        return (await taskA, await taskB, await taskC);
    }

    private IDictionary<Engine, List<DataPortTransformation>> TransformDataPorts(
        ClusterBuilder builder, IReadOnlyCollection<EngineHost> engineHosts)
    {
        try
        {
            return DataPortTransformer.TransformToFunctionBlocks(builder, engineHosts, rulesetProvider);
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
                WorkflowMetadata workflowMetadata;
                try
                {
                    (workflow, workflowMetadata) = ClusterToWorkflowTranslator.CreateWorkflow(cluster, desiredEngineHost, desiredEngine, dependencyResolver, brokerConnection, enginesDataPorts, _engineLogDirectory);
                }
                catch (Exception e)
                {
                    throw ClusterErrorInfos.CouldNotCreateDeployParameter().AsException(e);
                }
                var expectedEngineDeployment = expectedEngineHost?.Deployments.SingleOrDefault(e => e.EngineId == desiredEngine.Id);
                hostProvisioning.EngineActions.Add(new EngineProvisioning(desiredEngine, workflow, workflowMetadata, expectedEngineDeployment));
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
    private sealed record EngineProvisioning(Engine Desired, Workflow Workflow, WorkflowMetadata WorkflowMetadata, EngineDeployment? Expected = default) : IEngineAction;
    private sealed record EngineDisposal(EngineDeployment Expected) : IEngineAction;

    private static List<IEngineHostAction> OptimizeEngineHostActions(List<IEngineHostAction> engineHostActions)
    {
        List<IEngineHostAction> optimizedActions = [];
        foreach (var engineHostAction in engineHostActions)
        {
            if (engineHostAction is EngineHostProvisioning engineHostProvisioning)
            {
                List<IEngineAction> engineActions = [];
                foreach (var engineAction in engineHostProvisioning.EngineActions)
                {
                    // skip provisioning of engines without FunctionBlocks
                    if (engineAction is EngineProvisioning engineProvisioning &&
                        engineProvisioning.Workflow.FunctionBlocks.Count == 0)
                    {
                        // dispose engines without FunctionBlocks
                        if (engineProvisioning.Expected is not null)
                            engineActions.Add(new EngineDisposal(engineProvisioning.Expected));
                    }
                    else
                    {
                        engineActions.Add(engineAction);
                    }
                }

                // skip provisioning of engine host if no engines will be inside
                if (engineHostProvisioning.Expected is null && engineActions.Count == 0 && engineHostProvisioning.PhysicalState == EngineHostState.NotFound)
                    continue;

                // dispose engine host if no engines will be inside
                if ((engineHostProvisioning.Expected is not null || engineHostProvisioning.PhysicalState != EngineHostState.NotFound) &&
                    (engineActions.Count == 0 || engineActions.TrueForAll(static a => a is EngineDisposal)))
                {
                    optimizedActions.Add(new EngineHostDisposal(new EngineHosts.EngineHost() { Id = engineHostProvisioning.Desired.Id, }, engineHostProvisioning.PhysicalState)
                    {
                        EngineActions = [.. engineActions.OfType<EngineDisposal>(),],
                    });
                }
                else
                {
                    optimizedActions.Add(engineHostProvisioning with { EngineActions = engineActions, });
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

    private async Task ProvisionEngineHost(EngineHostProvisioning engineHostProvisioning, ConsumeContext context,
        CancellationToken cancellationToken)
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
                logger.LogDeployingEngineHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.Name);
                await poolCommunication.DeployHost(engineHostProvisioning.Desired.Id, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotDeployEngineHost(), cancellationToken);
                await poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                await poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                break;
            case EngineHostState.Started:
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                var appSettingsChanged = await poolCommunication.CompareHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotGetAppSettingsOfEngineHost(), cancellationToken) != 0;
                var elevatedPrivilegesChanged = false;
                if (engineHostProvisioning.Expected is not null)
                    elevatedPrivilegesChanged = engineHostProvisioning.Expected.ElevatedPrivileges != engineHostProvisioning.Desired.ElevatedPrivileges;
                if (appSettingsChanged || elevatedPrivilegesChanged)
                {
                    logger.LogStoppingEngineHost(engineHostProvisioning.Desired.Id);
                    await poolCommunication.StopHost(engineHostProvisioning.Desired.Id, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotStopEngineHost(), cancellationToken);

                    if (appSettingsChanged)
                    {
                        logger.LogUpdatingEngineHost(engineHostProvisioning.Desired.Id);
                        await poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                            .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                    }
                    if (elevatedPrivilegesChanged && engineHostProvisioning.Expected is not null)
                    {
                        engineHostProvisioning.Expected.ElevatedPrivileges = engineHostProvisioning.Desired.ElevatedPrivileges;
                        await engineDbContext.SaveChangesAsync(cancellationToken);
                    }

                    logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                    await poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                }
                else
                {
                    logger.LogSkipEngineHostUpdate(engineHostProvisioning.Desired.Id);
                }
                break;
            case EngineHostState.Stopped:
                await EnsureEngineHostInDatabase(engineHostProvisioning, cancellationToken);
                if (await poolCommunication.CompareHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotGetAppSettingsOfEngineHost(), cancellationToken) != 0)
                {
                    logger.LogUpdatingEngineHost(engineHostProvisioning.Desired.Id);
                    await poolCommunication.SetHostAppSettings(engineHostProvisioning.Desired.Id, engineHostProvisioning.AppSettings, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotSetAppSettingsOfEngineHost(), cancellationToken);
                }
                else
                {
                    logger.LogSkipEngineHostUpdate(engineHostProvisioning.Desired.Id);
                }
                logger.LogStartingEngineHost(engineHostProvisioning.Desired.Id);
                await poolCommunication.StartHost(engineHostProvisioning.Desired.Id, engineHostProvisioning.Desired.ElevatedPrivileges, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngineHost(), cancellationToken);
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(engineHostProvisioning.PhysicalState), (int)engineHostProvisioning.PhysicalState, typeof(EngineHostState));
        }

        async Task EnsureEngineHostInDatabase(EngineHostProvisioning ehProvisioning, CancellationToken cancelToken)
        {
            if (ehProvisioning.Expected is null)
            {
                engineDbContext.EngineHosts.Add(new()
                {
                    Id = ehProvisioning.Desired.Id,
                    ElevatedPrivileges = ehProvisioning.Desired.ElevatedPrivileges,
                });
                await engineDbContext.SaveChangesAsync(cancelToken);
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
                                logger.LogSkipEngineUpdate(desired.Id, expected.EngineHostId, expected.Id);
                            }
                            else
                            {
                                if (desired.Enabled && actualState == EngineState.Stopped)
                                    actions.Start.Add((expected.Id, expected.EngineId));
                                else if (!desired.Enabled)
                                    actions.Stop.Add((expected.Id, expected.EngineId, actualState == EngineState.Running, raiseStop));
                            }
                        }
                        else
                        {
                            logger.LogUpdatingEngine(desired.Id, expected.EngineHostId, expected.Id);
                            actions.ShouldNotRun.Add(expected);
                            actions.Stop.Add((expected.Id, expected.EngineId, actualState == EngineState.Running, raiseStop));
                            actions.Delete.Add(expected);
                            actions.TearDown.Add((expected.Id, expected.EngineId, true, true));
                            actions.Deploy.Add((desired, engineProvisioning.Workflow, engineProvisioning.WorkflowMetadata));
                        }
                    }
                    else
                    {
                        logger.LogRestoreMissingEngine(desired.Id, expected.EngineHostId, expected.Id);
                        actions.Delete.Add(expected);
                        actions.Deploy.Add((desired, engineProvisioning.Workflow, engineProvisioning.WorkflowMetadata));
                    }
                }
                else
                {
                    actions.Deploy.Add((desired, engineProvisioning.Workflow, engineProvisioning.WorkflowMetadata));
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
                        actions.Stop.Add((expected.Id, expected.EngineId, true, true));
                    }
                }
                actions.Delete.Add(expected);
                actions.TearDown.Add((expected.Id, expected.EngineId, true, true));
            }
        }
        foreach (var actualState in physicalDeploymentStates)
        {
            logger.LogRemoveUnexpectedEngine(engineHostProvisioning.Desired.Id, actualState.Key);
            if (actualState.Value == EngineState.Running)
                actions.Stop.Add((actualState.Key, Guid.Empty, true, false));
            actions.TearDown.Add((actualState.Key, Guid.Empty, true, false));
        }
        return actions;
    }

    private async Task DisposeEngineHost(EngineHostDisposal engineHostDisposal, ConsumeContext context,
        CancellationToken cancellationToken)
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
                actions.Stop.Add((engineDisposal.Expected.Id, engineDisposal.Expected.EngineId, execute, raiseEvent));
            }

            actions.Delete.AddRange(engineHostDisposal.EngineActions.Select(e => e.Expected));

            foreach (var engineDisposal in engineHostDisposal.EngineActions)
            {
                var execute = false;
                if (physicalDeploymentStates.Remove(engineDisposal.Expected.Id, out _))
                    execute = true;
                actions.TearDown.Add((engineDisposal.Expected.Id, engineDisposal.Expected.EngineId, execute, true));
            }
        }

        foreach (var deploymentState in physicalDeploymentStates)
        {
            if (deploymentState.Value == EngineState.Running)
                actions.Stop.Add((deploymentState.Key, Guid.Empty, true, false));
            actions.TearDown.Add((deploymentState.Key, Guid.Empty, true, false));
        }

        return actions;
    }

    private async Task DisposeEngineHost(EngineHostDisposal engineHostDisposal, CancellationToken cancellationToken)
    {
        if (engineHostDisposal.PhysicalState == EngineHostState.Started)
        {
            logger.LogStoppingEngineHost(engineHostDisposal.Expected.Id);
            await poolCommunication.StopHost(engineHostDisposal.Expected.Id, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotStopEngineHost(), cancellationToken);
        }

        if (engineHostDisposal.PhysicalState != EngineHostState.NotFound)
        {
            logger.LogTearingDownEngineHost(engineHostDisposal.Expected.Id);
            await poolCommunication.TearDownHost(engineHostDisposal.Expected.Id, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotTearDownEngineHost(), cancellationToken);
        }

        var dbEngineHost = await engineDbContext.EngineHosts.SingleOrDefaultAsync(eh => eh.Id == engineHostDisposal.Expected.Id, cancellationToken);
        if (dbEngineHost is not null)
        {
            engineDbContext.EngineHosts.Remove(dbEngineHost);
            await engineDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, EngineState>> GetDeploymentStates(Guid engineHostId, EngineHostState physicalState,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, EngineState> deploymentStates = [];
        if (physicalState == EngineHostState.Started)
        {
            deploymentStates = new(await hostCommunication.GetDeploymentStates(engineHostId, cancellationToken)
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
            await engineDbContext.SaveChangesAsync(cancellationToken);
        }

        if (actions.Stop.Count != 0)
        {
            foreach (var stop in actions.Stop)
            {
                logger.LogStoppingEngine(stop.EngineId, engineHostId, stop.DeploymentId);
                var stopped = !stop.Execute && stop.RaiseEvent;
                if (stop.Execute && await hostCommunication.StopEngine(engineHostId, stop.DeploymentId, cancellationToken)
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
            foreach (var delete in actions.Delete)
                engineDbContext.EngineDeployments.Remove(delete);
            await engineDbContext.SaveChangesAsync(cancellationToken);
        }

        if (actions.TearDown.Count != 0)
        {
            foreach (var tearDown in actions.TearDown)
            {
                logger.LogTearingDownEngine(tearDown.EngineId, engineHostId, tearDown.DeploymentId);
                var tornDown = !tearDown.Execute && tearDown.RaiseEvent;
                if (tearDown.Execute)
                {
                    await hostCommunication.TearDownEngine(engineHostId, tearDown.DeploymentId, cancellationToken)
                        .CatchAs(ClusterErrorInfos.CouldNotTearDownEngine(), cancellationToken);
                    tornDown = true;
                }
                if (tearDown.RaiseEvent && tornDown)
                    await context.Publish(new EngineTornDown(tearDown.EngineId), cancellationToken);
            }
        }

        foreach (var deploy in actions.Deploy)
        {
            logger.LogDeployingEngine(deploy.Engine.Id, deploy.Engine.Name, engineHostId, engineHostName);
            var deploymentIdentifier = await hostCommunication.DeployEngine(engineHostId, deploy.Workflow, cancellationToken)
                .CatchAs(ClusterErrorInfos.CouldNotDeployEngine(), cancellationToken);

            EngineDeployment engineDeployment = new()
            {
                Id = deploymentIdentifier,
                EngineHostId = engineHostId,
                EngineId = deploy.Engine.Id,
                Checksum = new WorkflowComparer().GetHashCode(deploy.Workflow),
                MqttRetainTopics = deploy.WorkflowMetadata.MqttRetainTopics,
            };
            engineDbContext.EngineDeployments.Add(engineDeployment);
            await engineDbContext.SaveChangesAsync(cancellationToken);

            if (deploy.Engine.Enabled)
            {
                actions.ShouldRun.Add(engineDeployment);
                actions.Start.Add((engineDeployment.Id, engineDeployment.EngineId));
            }
        }

        if (actions.ShouldRun.Count != 0)
        {
            foreach (var shouldRun in actions.ShouldRun)
                shouldRun.ShouldRun = true;
            await engineDbContext.SaveChangesAsync(cancellationToken);
        }

        if (actions.Start.Count != 0)
        {
            foreach (var start in actions.Start)
            {
                logger.LogStartingEngine(start.EngineId, engineHostId, start.DeploymentId);
                if (await hostCommunication.StartEngine(engineHostId, start.DeploymentId, cancellationToken)
                    .CatchAs(ClusterErrorInfos.CouldNotStartEngine(), cancellationToken))
                {
                    await context.Publish(new EngineStarted(start.EngineId), cancellationToken);
                }
            }
        }
    }

    private sealed class DeploymentActions
    {
        public List<EngineDeployment> ShouldNotRun { get; init; } = [];
        public List<(Guid DeploymentId, Guid EngineId, bool Execute, bool RaiseEvent)> Stop { get; init; } = [];
        public List<EngineDeployment> Delete { get; init; } = [];
        public List<(Guid DeploymentId, Guid EngineId, bool Execute, bool RaiseEvent)> TearDown { get; init; } = [];
        public List<(Engine Engine, Workflow Workflow, WorkflowMetadata WorkflowMetadata)> Deploy { get; init; } = [];
        public List<EngineDeployment> ShouldRun { get; init; } = [];
        public List<(Guid DeploymentId, Guid EngineId)> Start { get; init; } = [];
    }

    private static List<Guid> GetExpectedRetainMessages(IReadOnlyCollection<EngineHosts.EngineHost> expectedEngineHosts)
        => [.. expectedEngineHosts.SelectMany(eh => eh.Deployments.SelectMany(d => d.MqttRetainTopics)).Distinct(),];

    private async Task DeleteRetainMessages(IMqttClient mqttClient, IReadOnlyCollection<Guid> expectedRetainMessages, CancellationToken cancellationToken)
    {
        var finalEngineHosts = await engineDbContext.EngineDeployments.AsNoTracking().ToListAsync(cancellationToken);
        var retainMessageToRemove = expectedRetainMessages.Except(finalEngineHosts.SelectMany(d => d.MqttRetainTopics).Distinct());
        foreach (var topic in retainMessageToRemove)
        {
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic.ToString())
                .WithRetainFlag();
            await mqttClient.PublishAsync(builder.Build(), cancellationToken);
        }
    }

    private void LogDeploymentVolume(List<EngineHost> engineHosts, ClusterCache cache)
    {
        var engines = 0;
        var dataPorts = 0;
        var functionBlocks = 0;
        foreach (var engineHost in engineHosts)
        {
            engines += engineHost.Engines.Count;
            foreach (var engine in engineHost.Engines)
            {
                if (cache.EngineDataPorts.TryGetValue(engine, out var dps))
                    dataPorts += dps.Count;
                if (cache.EngineFunctionBlocks.TryGetValue(engine, out var fbs))
                    functionBlocks += fbs.Count;
            }
        }
        logger.LogDeploymentVolume(engineHosts.Count, engines, functionBlocks, dataPorts);
    }
}
