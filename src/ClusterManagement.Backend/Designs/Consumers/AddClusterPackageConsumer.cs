using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class AddClusterPackageConsumer(IPackagesStore packageStorage) :
    IConsumer<AddClusterPackage>
{
    public async Task Consume(ConsumeContext<AddClusterPackage> context)
    {
        try
        {
            // We get a zip archive as byte array and try to add it to our package storage.
            var added = await packageStorage.AddPackageManually(context.Message.Data, context.CancellationToken);

            await context.Publish(new ClusterPackageChanged(added.Name, added.Version.ToString(), ClusterPackageChange.Add) { CorrelationId = context.Message.CorrelationId });
        }
        catch (Exception ex)
        {
            var error = new ErrorInfo(200, ex.Message);

            await context.Publish(new ClusterPackageChangeFailed(context.Message.CorrelationId, error, ClusterPackageChange.Add));
        }
    }
}
