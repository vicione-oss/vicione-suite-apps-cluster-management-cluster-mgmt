using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Iodds.Commands;
using MassTransit;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class DeleteClusterPackageConsumer(IPackagesStore packageStorage) :
    IConsumer<DeleteClusterPackage>
{
    public async Task Consume(ConsumeContext<DeleteClusterPackage> context)
    {
        try
        {
            var dependency = new ClusterDependency
            {
                Name = context.Message.PackageName,
                Version = new Version(context.Message.Version)
            };

            await packageStorage.DeletePackage(dependency, context.CancellationToken);

            await context.Publish(new ClusterPackageChanged(context.Message.PackageName, context.Message.Version, ClusterPackageChange.Delete) { CorrelationId = context.Message.CorrelationId });
        }
        catch (Exception ex)
        {
            var error = new ErrorInfo(200, ex.Message);

            await context.Publish(new ClusterPackageChangeFailed(context.Message.CorrelationId, error, ClusterPackageChange.Delete));
        }
    }
}
