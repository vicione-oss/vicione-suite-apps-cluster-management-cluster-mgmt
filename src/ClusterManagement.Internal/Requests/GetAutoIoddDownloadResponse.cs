using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests
{
    public sealed class GetAutoIoddDownloadResponse : IResponse
    {
        public ErrorInfo? RequestError { get; set; }
        public bool IsAutoDownload { get; set; }
    }
}
