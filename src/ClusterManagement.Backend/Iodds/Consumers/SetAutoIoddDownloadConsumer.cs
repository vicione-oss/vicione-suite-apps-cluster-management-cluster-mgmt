using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class SetAutoIoddDownloadConsumer(IIoddStore ioddStore, ILogger<SetAutoIoddDownloadConsumer> logger) :
    IConsumer<SetAutoIoddDownload>
{
    public async Task Consume(ConsumeContext<SetAutoIoddDownload> context)
    {
        try
        {
            await ioddStore.SetAutoDownloadIodds(context.Message.Value, context.CancellationToken);
            var changeEvent = new AutoIoddDownloadChanged(context.Message.Value)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Value);

            var errorInfo = new ErrorInfo(100, ex.Message);
            var changeEvent = new AutoIoddDownloadChanged(context.Message.Value, errorInfo)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to set auto IODD download to {Value}")]
    private static partial void LogError(ILogger<SetAutoIoddDownloadConsumer> logger, bool value);
}
