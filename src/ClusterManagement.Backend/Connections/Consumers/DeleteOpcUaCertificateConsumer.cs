using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

public sealed partial class DeleteOpcUaCertificateConsumer(
    IOpcUaCertificateStore certificateStore,
    ILogger<DeleteOpcUaCertificateConsumer> logger) :
    RequestConsumer<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>
{
    public override Task<DeleteOpcUaCertificateResponse> Respond(DeleteOpcUaCertificateRequest message, CancellationToken cancellationToken)
    {
        certificateStore.DeleteCertificate(message.StorePath, message.Thumbprint);

        return Task.FromResult(new DeleteOpcUaCertificateResponse());
    }

    public override Task<DeleteOpcUaCertificateResponse> HandleException(DeleteOpcUaCertificateRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, message.Thumbprint, message.StorePath);

        return Task.FromResult(new DeleteOpcUaCertificateResponse { RequestError = OpcUaCertificateErrorInfo.Create(e) });
    }

    [LoggerMessage(LogLevel.Error, "Failed to delete the OPC UA certificate '{Thumbprint}' from store '{StorePath}'")]
    private static partial void LogError(ILogger logger, Exception exception, string thumbprint, string storePath);
}
