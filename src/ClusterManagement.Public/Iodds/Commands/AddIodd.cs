using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Commands;

public sealed record AddIodd(string Filename) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
