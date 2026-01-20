using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Requests;

public sealed class DownloadIoddResponse : IResponse
{
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[] Content { get; set; } = [];
#pragma warning restore CA1819 // Properties should not return arrays

    public string Filename { get; set; } = string.Empty;
    public ErrorInfo? RequestError { get; set; }
}
