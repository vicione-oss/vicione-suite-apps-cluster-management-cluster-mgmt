using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Consumers;

public sealed class CommitClusterChangeConsumer(
    IClusterSnapshotStore snapshotStore,
    IDesignProvider designProvider,
    ISuiteRulesetProvider rulesetProvider,
    ILogger<CommitClusterChangeConsumer> logger) : IConsumer<CommitClusterChange>, IConsumer<Fault<CommitClusterChange>>
{
    public async Task Consume(ConsumeContext<CommitClusterChange> context)
    {
        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} Id:{Id} Version:{Version}",
            nameof(CommitClusterChange),
            context.CorrelationId,
            context.Message.MetaInfo.ClusterId,
            context.Message.MetaInfo.ClusterVersion);

        var metaInfo = context.Message.MetaInfo;
        var version = metaInfo.ClusterVersion;

        if (context.Message.Options is not null)
        {
            var cluster = await ClusterSerializer.Decompress(context.Message.CompressedCluster) ??
                throw new InvalidOperationException($"Failed to deserialize cluster {metaInfo.ClusterId} v{metaInfo.ClusterVersion}");

            var modified = await ApplyClusterOptions(cluster, context.Message.Options, context.CancellationToken);
            version = modified.Version;

            await snapshotStore.Store(modified, context.CancellationToken);
        }
        else
        {
            var storedInfo = new ClusterSnapshotInfo(metaInfo.ClusterId, metaInfo.ClusterVersion, metaInfo.Name)
            {
                Description = metaInfo.Description
            };

            await snapshotStore.Store(context.Message.CompressedCluster, storedInfo, context.CancellationToken);
        }

        await context.Publish(
            new ClusterChanged(context.Message.CorrelationId, metaInfo.ClusterId, CrudAction.Created, version),
            context.CancellationToken);
    }

    private async Task<Cluster> ApplyClusterOptions(Cluster cluster, CommitClusterOptions options, CancellationToken cancellationToken)
    {
        if (options.IncrementVersion)
        {
            cluster.Version = await snapshotStore.GetLatestVersionIncrement(cluster.Id, options.IncrementType ?? VersionIncrementKind.Patch, cancellationToken);
            cluster.VersionCreated = DateTime.UtcNow;
            logger.LogDebug("Increased cluster {Id} version to v{Version}",
                cluster.Id,
                cluster.Version);
        }

        if (!options.CleanupDependencies)
            return cluster;

        // we need a builder to modify the cluster
        var designResolver = await designProvider.CreateResolver();
        using var builder = new ClusterBuilder(cluster, designResolver);

        // reduce dependencies to what is really used in the cluster
        var functionBlockDesignIds = builder.Cache.GetUsedFunctionBlockDesignIds().ToArray();

        // DataPort.DesignId is e.g. MQTT-Broker
        var dataPortDependencies = builder.Cache.DataPorts
            .Select(k => rulesetProvider.GetClusterDependency(k.RulesetId))
            .Union([rulesetProvider.GetSystemDataPortDependency()])
            .Distinct()
            .ToArray();

        // system dataport dependency won't be removed if it's added to the cluster as long as there's an mqtt dataport defined
        var usedDependencies = functionBlockDesignIds
            .Select(designResolver.ResolveFunctionBlockDesignDependency)
            .Union(dataPortDependencies)
            .ToArray();

        builder.Cluster.Dependencies.RemoveAll(k => !usedDependencies.Contains(k));

        return builder.Cluster;
    }

    /// <summary>
    /// called when an exception happened on consume and all retry attempts failed
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task Consume(ConsumeContext<Fault<CommitClusterChange>> context)
    {
        logger.LogError("{Command} faulted with errors: {Errors}",
            nameof(CommitClusterChange),
            string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        var metaInfo = context.Message.Message.MetaInfo;
        var correlationId = context.CorrelationId ?? throw new InvalidOperationException("CorrelationId is missing");

        var errorInfo = ClusterEventFactory.CreateErrorInfo(ClusterError.PublishFailed, metaInfo.ClusterId, metaInfo.ClusterVersion);
        await context.Publish(new CommitClusterChangeFailed(correlationId, metaInfo.ClusterId, metaInfo.ClusterVersion, errorInfo),
            context.CancellationToken);
    }
}
