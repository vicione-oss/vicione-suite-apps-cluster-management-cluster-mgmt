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
public sealed class StartEngineOnInstanceConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext,
    ILogger<StartEngineOnInstanceConsumer> logger) : IConsumer<StartEngineOnInstance>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;
    private readonly ILogger<StartEngineOnInstanceConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<StartEngineOnInstance> context)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == context.Message.EngineId, context.CancellationToken);
        if (deployment is not null)
        {
            _logger.LogStartingEngine(deployment.EngineId, deployment.EngineHostId, deployment.Id);
            if (await _engineHostCommunication.StartEngine(deployment.EngineHostId, deployment.Id, context.CancellationToken))
                await context.Publish(new EngineStarted(context.Message.EngineId), context.CancellationToken);
        }
    }
}
