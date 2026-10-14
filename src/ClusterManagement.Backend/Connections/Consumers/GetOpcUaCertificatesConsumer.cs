using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

public sealed partial class GetOpcUaCertificatesConsumer(
    IOpcUaCertificateStore certificateStore,
    ILogger<GetOpcUaCertificatesConsumer> logger) :
    RequestConsumer<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>
{
    public override Task<GetOpcUaCertificatesResponse> Respond(GetOpcUaCertificatesRequest message, CancellationToken cancellationToken)
        => Task.FromResult(new GetOpcUaCertificatesResponse(certificateStore.GetCertificates(message.StorePath)));

    public override Task<GetOpcUaCertificatesResponse> HandleException(GetOpcUaCertificatesRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, message.StorePath);

        return Task.FromResult(new GetOpcUaCertificatesResponse([]) { RequestError = OpcUaCertificateErrorInfo.Create(e) });
    }

    [LoggerMessage(LogLevel.Error, "Failed to get the OPC UA certificates of store '{StorePath}'")]
    private static partial void LogError(ILogger logger, Exception exception, string storePath);
}
