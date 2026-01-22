using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

#pragma warning disable CA1819 // Properties should not return arrays
public sealed record AddClusterPackage(byte[] Data) : ICommand
#pragma warning restore CA1819 // Properties should not return arrays
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
