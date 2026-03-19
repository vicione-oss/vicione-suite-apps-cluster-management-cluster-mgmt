using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Events;

[ForwardToUI]
public sealed record IoddChanged(IoddId Iodd, DeviceInfo? DeviceInfo, CrudAction Action, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public const int UnknownError = 0;
    public const int DeleteIoddError = 101;
    public const int DownloadIoddError = 102;
    public const int ExtractZipError = 103;
}
