using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

public sealed partial class AddOpcUaApplicationCertificateConsumer(
    IOpcUaCertificateStore certificateStore,
    ILogger<AddOpcUaApplicationCertificateConsumer> logger) :
    RequestConsumer<AddOpcUaApplicationCertificateRequest, AddOpcUaApplicationCertificateResponse>
{
    public override async Task<AddOpcUaApplicationCertificateResponse> Respond(AddOpcUaApplicationCertificateRequest message, CancellationToken cancellationToken)
    {
        var result = await certificateStore.AddApplicationCertificate(
            message.StorePath, message.Filename, message.Password, message.ApplicationCertificateSubject, message.ApplicationUri, cancellationToken);

        return new(result.Certificate, result.Warnings);
    }

    public override Task<AddOpcUaApplicationCertificateResponse> HandleException(AddOpcUaApplicationCertificateRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, message.Filename, message.StorePath);

        return Task.FromResult(new AddOpcUaApplicationCertificateResponse(null, []) { RequestError = OpcUaCertificateErrorInfo.Create(e) });
    }

    [LoggerMessage(LogLevel.Error, "Failed to add the OPC UA application certificate '{Filename}' to store '{StorePath}'")]
    private static partial void LogError(ILogger logger, Exception exception, string filename, string storePath);
}
