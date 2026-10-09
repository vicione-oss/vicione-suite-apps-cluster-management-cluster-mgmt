using ClusterManagement.Backend.DeployCluster.Contracts;
using MassTransit;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

/// <summary>
/// as for the AddClusterNodesConsumer we need to use the mediator to send
/// the messages one by one. Using the context all messages will be pushed
/// to the bus when the consumer has finished
/// </summary>
/// <remarks>Placeholder: answers per node without touching infrastructure; the Docker/Kubernetes integration replaces it.</remarks>
public sealed class CleanClusterNodesConsumer(ISuiteMediator mediator) : IConsumer<CleanClusterNodesCommand>
{
    private readonly ISuiteMediator _mediator = mediator;

    //5a. Update Cluster nodes (again)
    //5b. Remove unused Cluster nodes
    public async Task Consume(ConsumeContext<CleanClusterNodesCommand> context)
    {
        // ensure the saga can proceed if there's nothing to be cleaned up
        if (context.Message.NodesToClean.Count <= 0)
            throw new InvalidOperationException("No nodes to be cleaned up!");

        foreach (var nodeId in context.Message.NodesToClean)
        {
            await Task.Delay(200);
            await _mediator.Publish(new ClusterNodeCleanedEvent(context.Message.CorrelationId, context.Message.ClusterId, nodeId));
        }
    }
}
