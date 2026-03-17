using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class AddClusterPackageConsumer(IPackageStore packageStorage, ILogger<AddClusterPackageConsumer> logger) :
    IConsumer<AddClusterPackage>
{
    public async Task Consume(ConsumeContext<AddClusterPackage> context)
    {
        try
        {
            // We get a zip archive as byte array and try to add it to our package storage.
            var added = await packageStorage.StorePackage(context.Message.Data, true, context.CancellationToken);

            await context.Publish(new ClusterPackageChanged(added.Name, added.Version.ToString(), ClusterPackageChange.Add) { CorrelationId = context.Message.CorrelationId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add cluster package: {Error}", ex.Message);

            var error = new ErrorInfo(200, ex.Message);

            await context.Publish(new ClusterPackageChanged("Unknown", "0.0.0", ClusterPackageChange.Add, error) { CorrelationId = context.Message.CorrelationId });
        }
    }
}
