using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

public sealed partial class AddOpcUaCertificateConsumer(
    IOpcUaCertificateStore certificateStore,
    ILogger<AddOpcUaCertificateConsumer> logger) :
    RequestConsumer<AddOpcUaCertificateRequest, AddOpcUaCertificateResponse>
{
    public override async Task<AddOpcUaCertificateResponse> Respond(AddOpcUaCertificateRequest message, CancellationToken cancellationToken)
    {
        var result = await certificateStore.AddCertificates(message.StorePath, message.Filename, cancellationToken);
        return new(result.Certificates, result.IssuerCertificatesLeftOut);
    }

    public override Task<AddOpcUaCertificateResponse> HandleException(AddOpcUaCertificateRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, message.Filename, message.StorePath);

        return Task.FromResult(new AddOpcUaCertificateResponse([], false) { RequestError = OpcUaCertificateErrorInfo.Create(e) });
    }

    [LoggerMessage(LogLevel.Error, "Failed to add the OPC UA certificate '{Filename}' to store '{StorePath}'")]
    private static partial void LogError(ILogger logger, Exception exception, string filename, string storePath);
}
