using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

[MessageEndpoint(Constants.DeployClusterState)]
public sealed record DeployCluster(Guid ClusterId, Version TargetVersion) : ICommand
{
    public Guid CorrelationId => ClusterId;

    public DeployClusterOptions? Options { get; init; }
}
