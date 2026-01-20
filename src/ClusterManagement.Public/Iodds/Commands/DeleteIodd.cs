using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Commands;

public sealed record DeleteIodd(int VendorId, int DeviceId) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
