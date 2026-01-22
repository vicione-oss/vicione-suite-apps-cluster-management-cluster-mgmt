using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record UpdateClusterDependencies(List<ClusterDependencyOperation> Operations) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
