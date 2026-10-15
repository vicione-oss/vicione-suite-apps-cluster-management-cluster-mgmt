using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using ClusterManagement.Public.Events;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class GetEngineStateOnInstanceConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext)
    : InstanceDependentRequestConsumer<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;

    public override Task<GetEngineStateOnInstanceResponse> HandleException(GetEngineStateOnInstanceRequest message, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetEngineStateOnInstanceResponse(new ErrorInfo(e is ErrorInfoException known ? known.ErrorCode : ClusterError.UnknownError, e.Message)));

    public override async Task<GetEngineStateOnInstanceResponse> Respond(GetEngineStateOnInstanceRequest message, CancellationToken cancellationToken)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == message.EngineId, cancellationToken);
        if (deployment is not null)
        {
            try
            {
                var engineState = await _engineHostCommunication.GetEngineState(deployment.EngineHostId, deployment.Id, cancellationToken);

                return new()
                {
                    DeploymentId = deployment.Id,
                    EngineState = engineState switch
                    {
                        EngineState.Running => Internal.Requests.EngineState.Running,
                        EngineState.Stopped => Internal.Requests.EngineState.Stopped,
                        EngineState.Starting => Internal.Requests.EngineState.Starting,
                        EngineState.Stopping => Internal.Requests.EngineState.Stopping,
                        EngineState.StartFailed => Internal.Requests.EngineState.StartFailed,
                        _ => null
                    }
                };
            }
            catch (Exception ex)
            {
                // The deployment id lets the caller match the engine's cycle reports while its host restarts.
                return new(new ErrorInfo(ClusterErrors.EngineHostDidNotAnswer, $"Cannot retrieve state for engine '{message.EngineId}': {ex.Message}"))
                {
                    DeploymentId = deployment.Id,
                };
            }
        }

        throw new ErrorInfoException(ClusterErrors.EngineNotDeployed, $"Cannot find engine '{message.EngineId}'.");
    }
}
