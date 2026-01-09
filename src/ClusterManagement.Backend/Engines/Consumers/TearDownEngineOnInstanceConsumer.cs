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
public sealed class TearDownEngineOnInstanceConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext,
    ILogger<TearDownEngineOnInstanceConsumer> logger) : IConsumer<TearDownEngineOnInstance>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;
    private readonly ILogger<TearDownEngineOnInstanceConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<TearDownEngineOnInstance> context)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == context.Message.EngineId, context.CancellationToken);
        if (deployment is not null)
        {
            _logger.LogTearingDownEngine(deployment.EngineId, deployment.EngineHostId, deployment.Id);
            await _engineHostCommunication.TearDownEngine(deployment.EngineHostId, deployment.Id, context.CancellationToken);
            await context.Publish(new EngineTornDown(context.Message.EngineId), context.CancellationToken);
        }
    }
}
