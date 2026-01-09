using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record AddClusterNodesCommand(Guid ClusterId, Dictionary<Guid, Guid?> NodesToCreate) : ICommand
{
    public Guid CorrelationId => ClusterId;
}
