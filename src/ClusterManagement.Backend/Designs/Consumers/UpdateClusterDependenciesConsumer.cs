using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed partial class UpdateClusterDependenciesConsumer(IClusterDependencyStore dependencyStore, ILogger<UpdateClusterDependenciesConsumer> logger) :
    IConsumer<UpdateClusterDependencies>
{
    public async Task Consume(ConsumeContext<UpdateClusterDependencies> context)
    {
        try
        {
            var changes = await dependencyStore.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            var changeEvent = new ClusterDependenciesChanged(changes)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Operations.Count);

            var error = new ErrorInfo(230, ex.Message);
            var changeEvent = new ClusterDependenciesChanged([], error)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to enqueue {Count} package operations.")]
    private static partial void LogError(ILogger<UpdateClusterDependenciesConsumer> logger, int count);
}
