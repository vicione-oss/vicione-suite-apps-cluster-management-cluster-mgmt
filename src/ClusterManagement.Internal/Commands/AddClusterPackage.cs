using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record AddClusterPackage(string Filename) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
