using ClusterManagement.Backend.DeployCluster.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

/// <summary>
/// if we use the context.Publish(..) all events get pushed to the message bus
/// after the consumer is finished. To send them independently we have to use
/// the mediator. Otherwise the saga rans into concurrency issues!
/// </summary>
public sealed class AddClusterNodesConsumer(ISuiteMediator mediator, ILogger<AddClusterNodesConsumer> logger) :
    IConsumer<AddClusterNodesCommand>
{
    //1. Add+Update Cluster nodes
    public async Task Consume(ConsumeContext<AddClusterNodesCommand> context)
    {
        // should not happen but otherwise saga would die here
        if (context.Message.NodesToCreate.Count <= 0)
            throw new InvalidOperationException("No nodes to be created!");

        logger.LogInformation("Consuming {Command} - create nodes:{NodeIds} create apps:{AppIds}",
            nameof(AddClusterNodesCommand),
            string.Join(", ", context.Message.NodesToCreate.Keys),
            string.Join(", ", context.Message.NodesToCreate.Values));

        foreach (var (nodeId, applicationId) in context.Message.NodesToCreate)
        {
            //Talk to Kubernetes here!
            await Task.Delay(200, context.CancellationToken);

            logger.LogInformation("Consuming {Command} - publishing {Event} for node:{NodeId}",
                nameof(AddClusterNodesCommand),
                nameof(ClusterNodeAddedEvent),
                nodeId);

            //The following will need to be moved to a Kubernetes Watcher See: https://github.com/kubernetes-client/csharp/blob/master/examples/watch/Program.cs
            //To correlate the message to the saga the watcher will need to load the clusterId of the node via the cluster-repo
            await mediator.Publish(new ClusterNodeAddedEvent(context.Message.CorrelationId, context.Message.ClusterId, nodeId));

            //The following will need to be moved to "Consume(ConsumeContext<InstanceCreatedEvent> context)"
            //To correlate the message to the saga the consumer will need to load the clusterId of the application via the cluster-repo
            if (applicationId is not null)
            {
                await Task.Delay(200, context.CancellationToken);

                logger.LogInformation("Consuming {Command} - publishing {Event} for application:{NodeId}",
                    nameof(AddClusterNodesCommand),
                    nameof(ClusterApplicationStartedEvent),
                    nodeId);

                await mediator.Publish(new ClusterApplicationStartedEvent(context.Message.ClusterId, applicationId.Value)
                {
                    CorrelationId = context.Message.CorrelationId,
                });
            }
        }
    }
}
