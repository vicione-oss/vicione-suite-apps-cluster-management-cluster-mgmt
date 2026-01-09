using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

public sealed record RemoveCluster(Guid ClusterId, Version Version) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
