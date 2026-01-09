using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Internal.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class GetEngineStateOnInstanceConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext)
    : InstanceDependentRequestConsumer<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;

    protected override Task<GetEngineStateOnInstanceResponse> HandleException(ConsumeContext<GetEngineStateOnInstanceRequest> context, Exception e)
        => Task.FromResult(new GetEngineStateOnInstanceResponse(new ErrorInfo(((ErrorInfoException)e).ErrorCode, e.Message)));

    protected override async Task<GetEngineStateOnInstanceResponse> Respond(ConsumeContext<GetEngineStateOnInstanceRequest> context)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == context.Message.EngineId, context.CancellationToken);
        if (deployment is not null)
        {
            try
            {
                var engineState = await _engineHostCommunication.GetEngineState(deployment.EngineHostId, deployment.Id, context.CancellationToken);

                return new()
                {
                    EngineState = engineState switch
                    {
                        EngineState.Running => Internal.Requests.EngineState.Running,
                        EngineState.Stopped => Internal.Requests.EngineState.Stopped,
                        EngineState.Starting => Internal.Requests.EngineState.Starting,
                        EngineState.Stopping => Internal.Requests.EngineState.Stopping,
                        _ => null
                    }
                };
            }
            catch (Exception ex)
            {
                throw new ErrorInfoException($"Cannot retrieve state for engine '{context.Message.EngineId}'.", ex);
            }
        }

        throw new ErrorInfoException($"Cannot find engine '{context.Message.EngineId}'.");
    }
}
