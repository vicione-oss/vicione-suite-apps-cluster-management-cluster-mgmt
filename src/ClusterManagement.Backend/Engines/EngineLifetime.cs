using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace ClusterManagement.Backend.Engines;

internal sealed class EngineLifetime(IEngineHostPoolCommunication engineHostPoolCommunication,
    IEngineHostCommunication engineHostCommunication,
    IEngineDbContext engineDbContext,
    IClusterSagaDbContext sagaDbContext,
    ISuiteMediator mediator,
    IInstanceInformationProvider instanceInformationProvider,
    ILogger<EngineLifetime> logger) : IEngineLifetime
{
    public async Task Start(CancellationToken cancellationToken)
    {
        if (instanceInformationProvider.Local.Type == InstanceType.Standalone)
        {
            var clusterInfos = await mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(new(), cancellationToken);
            if (clusterInfos.RequestError is null)
            {
                var activeCluster = clusterInfos.Results.SingleOrDefault(clusterInfo => clusterInfo.IsActive);
                if (activeCluster is not null)
                {
                    await RemoveExistingSagaState(activeCluster.Id, cancellationToken);

                    Public.Commands.DeployCluster deployCluster = new(activeCluster.Id, activeCluster.Version);
                    await mediator.Send(deployCluster, cancellationToken);
                }
            }
        }
        else
        {
            var engineHosts = await engineDbContext.EngineHosts.AsNoTracking()
                .Include(eh => eh.Deployments.Where(d => d.ShouldRun)).AsSplitQuery()
                .Where(eh => eh.Deployments.Count > 0).ToListAsync(cancellationToken);
            var startEngineHosts = engineHosts.Select(engineHost => StartEngineHost(engineHost, cancellationToken));
            await Task.WhenAll(startEngineHosts);
        }
    }

    private async Task StartEngineHost(EngineHost engineHost, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogStartingEngineHost(engineHost.Id);
            await engineHostPoolCommunication.StartHost(engineHost.Id, engineHost.ElevatedPrivileges, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogStartingEngineHostFailed(ex, engineHost.Id);
            return;
        }

        if (await engineHostPoolCommunication.GetState(engineHost.Id, cancellationToken) != EngineHostState.NotFound)
        {
            var startEngines = engineHost.Deployments.Select(deployment => StartEngine(deployment, cancellationToken));
            await Task.WhenAll(startEngines);
        }
    }

    private async Task StartEngine(EngineDeployment deployment, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogStartingEngine(deployment.EngineId, deployment.EngineHostId, deployment.Id);
            if (await engineHostCommunication.StartEngine(deployment.EngineHostId, deployment.Id, cancellationToken))
                await mediator.Publish(new EngineStarted(deployment.EngineId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogStartingEngineFailed(ex, deployment.EngineId, deployment.EngineHostId, deployment.Id);
        }
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        var engineHosts = await engineDbContext.EngineHosts.AsNoTracking().Include(eh => eh.Deployments).AsSplitQuery().ToListAsync(cancellationToken);
        var stopEngineHosts = engineHosts.Select(engineHost => StopEngineHost(engineHost, cancellationToken));
        await Task.WhenAll(stopEngineHosts);
    }

    private async Task StopEngineHost(EngineHost engineHost, CancellationToken cancellationToken)
    {
        var enginesCouldRun = await engineHostPoolCommunication.GetState(engineHost.Id, cancellationToken) == EngineHostState.Started;

        var stopEngines = engineHost.Deployments.Select(deployment => StopEngine(engineHost.Id, deployment.EngineId, deployment.Id, enginesCouldRun, cancellationToken));
        await Task.WhenAll(stopEngines);

        try
        {
            logger.LogStoppingEngineHost(engineHost.Id);
            await engineHostPoolCommunication.StopHost(engineHost.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogStoppingEngineHostFailed(ex, engineHost.Id);
        }
    }

    private async Task StopEngine(Guid engineHostId, Guid engineId, Guid deploymentId, bool enginesCouldRun, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogStoppingEngine(engineId, engineHostId, deploymentId);
            if (enginesCouldRun)
                await engineHostCommunication.StopEngine(engineHostId, deploymentId, cancellationToken);
            await mediator.Publish(new EngineStopped(engineId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogStoppingEngineFailed(ex, engineId, engineHostId, deploymentId);
        }
    }

    // Earlier versions could leave several deployments of one cluster behind; each of them would reject the next one.
    private async Task RemoveExistingSagaState(Guid clusterId, CancellationToken cancellationToken)
    {
        var deployStates = await sagaDbContext.DeployClusterState
                .Where(k => k.ClusterId == clusterId)
                .ToListAsync(cancellationToken);

        if (deployStates.Count == 0)
            return;

        logger.LogRemoveDeploySagaState(clusterId);

        sagaDbContext.DeployClusterState.RemoveRange(deployStates);
        await sagaDbContext.SaveChangesAsync(cancellationToken);
    }
}
