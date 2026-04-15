using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Factories;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Instance.Events;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class InstanceCreatedConsumer(IClusterSnapshotStore clusterSnapshotStore,
    IInstanceInformationProvider informationProvider,
    ISuiteMediator mediator,
    ILogger<InstanceCreatedConsumer> logger) : IConsumer<InstanceCreated>, IConsumer<InstanceSynchronizationFailed>
{
    public async Task Consume(ConsumeContext<InstanceCreated> context)
    {
        var clusters = (await clusterSnapshotStore.GetClusterInfos(null, context.CancellationToken)).ToArray();
        var clusterInfo = clusters.First(c => c.Version == clusters.Max(ci => ci.Version));
        var clusterId = clusterInfo.Id;
        var cluster = await clusterSnapshotStore.Load(clusterInfo.Id, clusterInfo.Version, context.CancellationToken) ??
            throw new InvalidOperationException($"The cluster {clusterId} cannot be loaded");

        var clusterNode = cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .FirstOrDefault(cn => cn.Applications.Any(a => a.Id == context.Message.InstanceId));

        if (clusterNode is not null)
        {
            LogClusterNodeExists(
                logger,
                nameof(InstanceCreated),
                context.Message.InstanceId,
                cluster.Id,
                cluster.Version);

            //cluster model is already fine. The instance is either restarted, or new and created from the saga
            await context.Publish(new ClusterApplicationStartedEvent(clusterId, context.Message.InstanceId)
            {
                CorrelationId = context.CorrelationId ?? Guid.NewGuid()
            });
            return;
        }

        var instances = await informationProvider.GetInstancesInCluster(context.CancellationToken);
        var instance = instances.FirstOrDefault(i => i.Id == context.Message.InstanceId) ??
            throw new InvalidOperationException($"The instance {context.Message.InstanceId} is created, but cannot be loaded");

        var newNode = ClusterNodeFactory.CreateApplicationNode(instance);
        cluster.NodeGroups[0].Nodes.Add(newNode); //currently only one node group. Better solution depends on how NodeGroups will be managed
        cluster.Description = $"Added node and application for instance {context.Message.InstanceId}";

        cluster.Version = cluster.Version.GetVersionIncrement(VersionIncrementKind.Patch);

        LogNewInstanceAdded(
            logger,
            nameof(InstanceCreated),
            instance.Type,
            instance.Name ?? "Unknown instance name",
            instance.Id,
            cluster.Id,
            cluster.Version);

        await mediator.Send(new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), null), context.CancellationToken);
    }

    public Task Consume(ConsumeContext<InstanceSynchronizationFailed> context)
    {
        LogInstanceSynchronizationFailed(logger, context.Message.InstanceId);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Debug, "Consumed {EventName}. Cluster model ok. Instance:{InstanceId} already exists in cluster:{ClusterId} v.{ClusterVersion}")]
    private static partial void LogClusterNodeExists(ILogger<InstanceCreatedConsumer> logger, string eventName, Guid instanceId, Guid clusterId, Version clusterVersion);

    [LoggerMessage(LogLevel.Information, "Consumed {EventName}. Added new {InstanceType} application for instance:{InstanceName} (id:{InstanceId}) to cluster:{ClusterId} v.{ClusterVersion}")]
    private static partial void LogNewInstanceAdded(ILogger<InstanceCreatedConsumer> logger, string eventName, InstanceType instanceType, string instanceName, Guid instanceId, Guid clusterId, Version clusterVersion);

    [LoggerMessage(LogLevel.Warning, "Failed to synchronize instance={InstanceId}")]
    private static partial void LogInstanceSynchronizationFailed(ILogger<InstanceCreatedConsumer> logger, Guid instanceId);
}
