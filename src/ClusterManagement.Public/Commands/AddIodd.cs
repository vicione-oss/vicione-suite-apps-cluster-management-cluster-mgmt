using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

[ForwardToUI]
#pragma warning disable CA1819 // Properties should not return arrays
public sealed record AddIodd(byte[] Data) : ICommand
#pragma warning restore CA1819 // Properties should not return arrays
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
