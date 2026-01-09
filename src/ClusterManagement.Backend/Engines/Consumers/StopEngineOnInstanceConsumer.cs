using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

[ReadOnlyConsumer]
public sealed class StopEngineOnInstanceConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext,
    ILogger<StopEngineOnInstanceConsumer> logger) : IConsumer<StopEngineOnInstance>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;
    private readonly ILogger<StopEngineOnInstanceConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<StopEngineOnInstance> context)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == context.Message.EngineId, context.CancellationToken);
        if (deployment is not null)
        {
            _logger.LogStoppingEngine(deployment.EngineId, deployment.EngineHostId, deployment.Id);
            if (await _engineHostCommunication.StopEngine(deployment.EngineHostId, deployment.Id, context.CancellationToken))
                await context.Publish(new EngineStopped(context.Message.EngineId), context.CancellationToken);
        }
    }
}
