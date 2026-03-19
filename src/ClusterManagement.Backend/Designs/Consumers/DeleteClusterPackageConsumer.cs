using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Iodds.Commands;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed partial class DeleteClusterPackageConsumer(IPackageStore packageStorage, ILogger<DeleteClusterPackageConsumer> logger) :
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

            var changeEvent = new ClusterPackageChanged(context.Message.PackageName, context.Message.Version, ClusterPackageChange.Delete)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.PackageName, context.Message.Version);

            var changeEvent = new ClusterPackageChanged(context.Message.PackageName, context.Message.Version, ClusterPackageChange.Delete)
            {
                CorrelationId = context.Message.CorrelationId,
                Error = new ErrorInfo(200, ex.Message)
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete cluster package {PackageName} version {Version}")]
    private static partial void LogError(ILogger<DeleteClusterPackageConsumer> logger, string packageName, string version);
}
