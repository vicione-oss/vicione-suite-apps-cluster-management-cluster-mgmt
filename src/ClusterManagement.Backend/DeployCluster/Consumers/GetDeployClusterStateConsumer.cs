using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed class GetDeployClusterStateConsumer(IClusterSagaDbContext sagaDbContext, ILogger<GetDeployClusterStateConsumer> logger) :
    RequestConsumer<GetDeployClusterStateRequest, GetDeployClusterStateResponse>
{
    public override async Task<GetDeployClusterStateResponse> Respond(GetDeployClusterStateRequest message, CancellationToken cancellationToken)
    {
        var deployState = await sagaDbContext.DeployClusterState
            .SingleOrDefaultAsync(k => k.ClusterId == message.ClusterId, cancellationToken);
        if (deployState is null)
            return new();

        return new()
        {
            State = new DeploymentState(
                deployState.ClusterId,
                DeployClusterStateMachine.GetState(deployState.CurrentState),
                deployState.ActiveVersion,
                deployState.TargetVersion)
        };
    }

    public override Task<GetDeployClusterStateResponse> HandleException(GetDeployClusterStateRequest message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetDeployClusterStateRequest));

        return Task.FromResult(new GetDeployClusterStateResponse
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
