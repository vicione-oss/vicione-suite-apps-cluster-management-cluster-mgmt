using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class UpdateClusterDependenciesConsumer(IClusterDependencyStore dependencyStore) :
    IConsumer<UpdateClusterDependencies>
{
    public async Task Consume(ConsumeContext<UpdateClusterDependencies> context)
    {
        try
        {
            await dependencyStore.EnqueueOperations(context.Message.Operations, context.CancellationToken);

            await context.Publish(new ClusterDependenciesUpdated(context.Message.Operations));
        }
        catch (Exception ex)
        {
            var error = new ErrorInfo(230, ex.Message);

            await context.Publish(new ClusterDependenciesUpdateFailed(context.Message.CorrelationId, error));
        }
    }
}
