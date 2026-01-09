using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record CleanClusterNodesCommand(Guid ClusterId, List<Guid> NodesToClean) : ICommand
{
    public Guid CorrelationId => ClusterId;
}
