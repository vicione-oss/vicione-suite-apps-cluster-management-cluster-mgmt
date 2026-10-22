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
    public const int IoddFileNotFound = 104;
    public const int IoddFileOutsideUploadDirectory = 105;
    /// <summary>
    /// A device with the same vendor and device id already exists; <see cref="Iodd"/> and <see cref="DeviceInfo"/> describe it.
    /// </summary>
    public const int IoddAlreadyExists = 106;
    /// <summary>
    /// The archive has more entries or extracts to more bytes than <c>MaxArchiveEntries</c> or <c>MaxArchiveExtractedSize</c> allow.
    /// </summary>
    public const int ArchiveTooLarge = 107;
}
