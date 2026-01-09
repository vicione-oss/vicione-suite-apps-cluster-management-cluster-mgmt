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

public sealed class InstanceCreatedConsumer(IClusterSnapshotStore clusterSnapshotStore,
    IInstanceInformationProvider informationProvider,
    ISuiteMediator mediator,
    ILogger<InstanceCreatedConsumer> logger) : IConsumer<InstanceCreated>, IConsumer<InstanceSynchronizationFailed>
{
    private readonly IClusterSnapshotStore _clusterSnapshotStore = clusterSnapshotStore;
    private readonly IInstanceInformationProvider _informationProvider = informationProvider;
    private readonly ISuiteMediator _mediator = mediator;
    private readonly ILogger<InstanceCreatedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<InstanceCreated> context)
    {
        var clusters = (await _clusterSnapshotStore.GetClusterInfos(null, context.CancellationToken)).ToArray();
        var clusterInfo = clusters.First(c => c.Version == clusters.Max(ci => ci.Version));
        var clusterId = clusterInfo.Id;
        var cluster = await _clusterSnapshotStore.Load(clusterInfo.Id, clusterInfo.Version, context.CancellationToken) ??
            throw new InvalidOperationException($"The cluster {clusterId} cannot be loaded");

        var clusterNode = cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .FirstOrDefault(cn => cn.Applications.Any(a => a.Id == context.Message.InstanceId));

        if (clusterNode is not null)
        {
            _logger.LogInformation("Consumed {Name}. Cluster model ok. Instance:{InstanceId} already exists in cluster:{ClusterId} v.{Version}",
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

        var instances = await _informationProvider.GetInstancesInCluster(context.CancellationToken);
        var instance = instances.FirstOrDefault(i => i.Id == context.Message.InstanceId) ??
            throw new InvalidOperationException($"The instance {context.Message.InstanceId} is created, but cannot be loaded");

        var newNode = ClusterNodeFactory.CreateApplicationNode(instance);
        cluster.NodeGroups[0].Nodes.Add(newNode); //currently only one node group. Better solution depends on how NodeGroups will be managed
        cluster.Description = $"Added node and application for instance {context.Message.InstanceId}";

        cluster.Version = cluster.Version.GetVersionIncrement(VersionIncrementKind.Patch);

        _logger.LogInformation("Consumed {Name}. Added new {Type} application for instance:{Instance} (id:{InstanceId}) to cluster:{ClusterId} v.{Version}",
            nameof(InstanceCreated),
            instance.Type,
            instance.Name,
            instance.Id,
            cluster.Id,
            cluster.Version);

        await _mediator.Send(new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), null), context.CancellationToken);
    }

    public Task Consume(ConsumeContext<InstanceSynchronizationFailed> context)
    {
        _logger.LogWarning("Failed to synchronize instance={InstanceId}", context.Message.InstanceId);
        return Task.CompletedTask;
    }
}
