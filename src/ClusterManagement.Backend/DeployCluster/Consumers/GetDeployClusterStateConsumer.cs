using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed class GetDeployClusterStateConsumer(IClusterSagaDbContext sagaDbContext, ILogger<GetDeployClusterStateConsumer> logger) :
    RequestConsumer<GetDeployClusterStateRequest, GetDeployClusterStateResponse>
{
    protected override async Task<GetDeployClusterStateResponse> Respond(ConsumeContext<GetDeployClusterStateRequest> context)
    {
        var deployState = await sagaDbContext.DeployClusterState
            .SingleOrDefaultAsync(k => k.ClusterId == context.Message.ClusterId, context.CancellationToken);
        if (deployState is null)
            return new();

        return new()
        {
            State = new DeploymentState(
                deployState.ClusterId,
                deployState.CurrentState,
                deployState.ActiveVersion,
                deployState.TargetVersion)
        };
    }

    protected override Task<GetDeployClusterStateResponse> HandleException(ConsumeContext<GetDeployClusterStateRequest> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetDeployClusterStateRequest));

        return Task.FromResult(new GetDeployClusterStateResponse
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
