using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record SetAutoIoddDownload(bool Value) : ICommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
