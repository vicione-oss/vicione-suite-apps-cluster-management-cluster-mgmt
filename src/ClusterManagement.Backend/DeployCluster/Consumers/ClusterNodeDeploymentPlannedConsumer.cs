using ClusterManagement.Public.Events;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

/// <summary>
/// Keeps the plans of the instances where the deployment state is asked for, so <see cref="GetDeployClusterStateConsumer"/>
/// can hand them to a client that starts watching a running deployment.
/// </summary>
/// <remarks>
/// Each instance answers the deployment state for its own clients, so each one keeps the plans of all instances.
/// </remarks>
[ReadOnlyConsumer]
public sealed class ClusterNodeDeploymentPlannedConsumer(DeploymentPlanStore store) : IConsumer<ClusterNodeDeploymentPlanned>
{
    public Task Consume(ConsumeContext<ClusterNodeDeploymentPlanned> context)
    {
        store.Keep(context.Message);
        return Task.CompletedTask;
    }
}

/// <remarks>
/// The instance endpoint takes one message at a time on a standalone or slave instance, so the plans would wait there
/// until the instance's own update ended. The plans live in memory only, so the endpoint goes away with the process.
/// </remarks>
public sealed class ClusterNodeDeploymentPlannedConsumerDefinition : ConsumerDefinition<ClusterNodeDeploymentPlannedConsumer>
{
    public ClusterNodeDeploymentPlannedConsumerDefinition()
        => Endpoint(e =>
        {
            e.Name = DefaultEndpointNameFormatter.Instance.TemporaryEndpoint("DeploymentPlans");
            e.Temporary = true;
        });
}
