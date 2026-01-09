using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record AddClusterNodesCommand(Guid CorrelationId, Guid ClusterId, Dictionary<Guid, Guid?> NodesToCreate) : ICommand;
