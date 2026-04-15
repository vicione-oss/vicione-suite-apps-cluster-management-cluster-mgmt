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
public sealed partial class AddClusterNodesConsumer(ISuiteMediator mediator, ILogger<AddClusterNodesConsumer> logger) :
    IConsumer<AddClusterNodesCommand>
{
    //1. Add+Update Cluster nodes
    public async Task Consume(ConsumeContext<AddClusterNodesCommand> context)
    {
        // should not happen but otherwise saga would die here
        if (context.Message.NodesToCreate.Count <= 0)
            throw new InvalidOperationException("No nodes to be created!");

        if (logger.IsEnabled(LogLevel.Information))
        {
            var nodeIds = string.Join(", ", context.Message.NodesToCreate.Keys);
            var appIds = string.Join(", ", context.Message.NodesToCreate.Values);
            LogConsumeCreatesNodesAndApps(
                logger,
                nameof(AddClusterNodesCommand),
                nodeIds,
                appIds);
        }

        foreach (var (nodeId, applicationId) in context.Message.NodesToCreate)
        {
            //Talk to Kubernetes here!
            await Task.Delay(200, context.CancellationToken);

            LogConsumePublishesEvent(
                logger,
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

                LogConsumePublishesEvent(
                    logger,
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Command} - create nodes:{NodeIds} create apps:{AppIds}")]
    private static partial void LogConsumeCreatesNodesAndApps(ILogger<AddClusterNodesConsumer> logger, string command, string nodeIds, string appIds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming {Command} - publishing {EventName} for node:{NodeId}")]
    private static partial void LogConsumePublishesEvent(ILogger<AddClusterNodesConsumer> logger, string command, string eventName, Guid nodeId);
}
