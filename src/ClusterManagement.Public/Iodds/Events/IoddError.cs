using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Events;

[ForwardToUI]
public sealed record IoddError(string Name, ErrorInfo Error) : IEvent
{
    public const int UnknownError = 0;
    public const int DeleteIoddError = 101;
    public const int DownloadIoddError = 102;
    public const int ExtractZipError = 103;
}
