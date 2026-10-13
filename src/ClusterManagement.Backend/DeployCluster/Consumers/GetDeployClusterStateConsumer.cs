using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed partial class GetDeployClusterStateConsumer(IClusterSagaDbContext sagaDbContext, ILogger<GetDeployClusterStateConsumer> logger) :
    RequestConsumer<GetDeployClusterStateRequest, GetDeployClusterStateResponse>
{
    public override async Task<GetDeployClusterStateResponse> Respond(GetDeployClusterStateRequest message, CancellationToken cancellationToken)
    {
        // Earlier versions could leave several deployments of one cluster behind; the newest one is reported.
        var deployState = await sagaDbContext.DeployClusterState
            .Where(k => k.ClusterId == message.ClusterId)
            .OrderByDescending(k => k.Started)
            .FirstOrDefaultAsync(cancellationToken);
        if (deployState is null)
            return new();

        return new()
        {
            State = new DeploymentState(
                deployState.ClusterId,
                DeployClusterStateMachine.GetState(deployState.CurrentState),
                deployState.ActiveVersion,
                deployState.TargetVersion)
            {
                CorrelationId = deployState.CorrelationId,
            },
        };
    }

    public override Task<GetDeployClusterStateResponse> HandleException(GetDeployClusterStateRequest message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogHandleException(e, nameof(GetDeployClusterStateRequest));

        return Task.FromResult(new GetDeployClusterStateResponse
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
