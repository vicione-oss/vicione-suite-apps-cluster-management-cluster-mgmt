using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record CleanClusterNodesCommand(Guid CorrelationId, Guid ClusterId, List<Guid> NodesToClean) : ICommand;
