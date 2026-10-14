using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

/// <summary>
/// Deletes the certificate stores of an OPC UA connection along with the connection.
/// </summary>
/// <remarks>
/// A DataPort that still uses the stores, e.g. in the running cluster, then creates a new application certificate and
/// trusts no certificates until the cluster is deployed without it.
/// </remarks>
public sealed partial class OpcUaConnectionDeletedConsumer(
    IOpcUaCertificateStore certificateStore,
    ILogger<OpcUaConnectionDeletedConsumer> logger) : IConsumer<ConnectionChanged>
{
    public Task Consume(ConsumeContext<ConnectionChanged> context)
    {
        if (context.Message.Action != CrudAction.Deleted)
            return Task.CompletedTask;

        try
        {
            certificateStore.DeleteConnectionStores(OpcUaStorePaths.GetStorePaths(context.Message.Connection));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // e.g. a file a running engine still holds; no connection uses the stores any more, so the cleanup at the
            // next module start deletes them, a retry of the message would not
            LogDeleteFailed(logger, e, context.Message.Connection.Name);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Warning, "Failed to delete the OPC UA certificate stores of the deleted connection '{Connection}'")]
    private static partial void LogDeleteFailed(ILogger logger, Exception exception, string? connection);
}
