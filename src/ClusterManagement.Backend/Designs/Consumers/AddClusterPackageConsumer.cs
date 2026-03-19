using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed partial class AddClusterPackageConsumer(IPackageStore packageStorage, ILogger<AddClusterPackageConsumer> logger) :
    IConsumer<AddClusterPackage>
{
    public async Task Consume(ConsumeContext<AddClusterPackage> context)
    {
        try
        {
            // We get a zip archive as byte array and try to add it to our package storage.
            var added = await packageStorage.StorePackage(context.Message.Data, true, context.CancellationToken);
            var changeEvent = new ClusterPackageChanged(added.Name, added.Version.ToString(), ClusterPackageChange.Add)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Data.Length);

            var error = new ErrorInfo(200, ex.Message);
            var changeEvent = new ClusterPackageChanged("Unknown", "0.0.0", ClusterPackageChange.Add, error)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add cluster package with length: {Length}")]
    private static partial void LogError(ILogger<AddClusterPackageConsumer> logger, int length);
}
