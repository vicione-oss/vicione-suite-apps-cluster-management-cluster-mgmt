using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Internal.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using System.Text.Json;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Extensions;
using ViciOne.Cluster.Model;
using ViciOne.Engine.DefaultPoolings;

namespace ClusterManagement.Backend.Consumers;

public sealed class CommitClusterChangeConsumer(
    IClusterSnapshotStore snapshotStore,
    IDesignProvider designProvider,
    ISuiteRulesetProvider rulesetProvider,
    ILogger<CommitClusterChangeConsumer> logger) : IConsumer<CommitClusterChange>, IConsumer<Fault<CommitClusterChange>>
{
    public async Task Consume(ConsumeContext<CommitClusterChange> context)
    {
        logger.LogConsumeCommand(
            nameof(CommitClusterChange),
            context.CorrelationId,
            context.Message.MetaInfo.ClusterId,
            context.Message.MetaInfo.ClusterVersion);

        var metaInfo = context.Message.MetaInfo;
        var metaVersion = metaInfo.ClusterVersion;

        // A retry cannot fix the command itself, so these failures are reported directly instead of faulting the message
        Cluster cluster;
        try
        {
            cluster = await ClusterSerializer.Decompress(context.Message.CompressedCluster, cancellationToken: context.CancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
        {
            logger.LogCommitClusterUnreadable(ex, context.Message.CorrelationId, metaInfo.ClusterId, metaInfo.ClusterVersion);

            await PublishCommitFailed(context, ClusterEventFactory.CreateErrorInfo(ClusterError.PublishFailed, metaInfo.ClusterId, metaInfo.ClusterVersion));
            return;
        }

        // The snapshot is stored under the cluster's own id, while the change is reported for the committed one
        if (cluster.Id != metaInfo.ClusterId)
        {
            logger.LogCommitClusterIdMismatch(context.Message.CorrelationId, metaInfo.ClusterId, metaInfo.ClusterVersion, cluster.Id);

            await PublishCommitFailed(context, new ErrorInfo(ClusterError.PublishFailedClusterIdMismatch,
                $"Cluster {cluster.Id} does not match the committed cluster {metaInfo.ClusterId}"));
            return;
        }

        if (context.Message.Options is not null)
        {
            var modified = await ApplyClusterOptions(cluster, context.Message.Options, context.CancellationToken);
            metaVersion = modified.Version;

            await snapshotStore.Store(modified, context.CancellationToken);
        }
        else
        {
            if (metaVersion > cluster.Version)
            {
                cluster.Version = metaVersion;
                cluster.VersionCreated = DateTime.UtcNow;
                logger.LogSetClusterVersion(cluster.Id, cluster.Version);
            }

            await snapshotStore.Store(cluster, context.CancellationToken);
        }

        await context.Publish(
            new ClusterChanged(context.Message.CorrelationId, metaInfo.ClusterId, CrudAction.Created, metaVersion),
            context.CancellationToken);
    }

    private async Task<Cluster> ApplyClusterOptions(Cluster cluster, CommitClusterOptions options, CancellationToken cancellationToken)
    {
        if (options.IncrementVersion)
            await IncrementVersion(cluster, options, cancellationToken);

        var designResolver = await designProvider.CreateResolver();
        using ClusterBuilder builder = new(cluster, designResolver);

        if (options.CleanupDependencies)
            CleanupDependencies(builder);

        EnsureDefaultPoolingsDependency(builder);

        return builder.Cluster;
    }

    private async Task IncrementVersion(Cluster cluster, CommitClusterOptions options, CancellationToken cancellationToken)
    {
        cluster.Version = await snapshotStore.GetLatestVersionIncrement(cluster.Id, options.IncrementType ?? VersionIncrementKind.Patch, cancellationToken);
        cluster.VersionCreated = DateTime.UtcNow;
        logger.LogIncreasedClusterVersion(cluster.Id, cluster.Version);
    }

    private void CleanupDependencies(ClusterBuilder builder)
    {
        // reduce dependencies to what is really used in the cluster
        var functionBlockDesignIds = builder.Cache.GetUsedFunctionBlockDesignIds().ToArray();

        // DataPort.DesignId is e.g. MqttClientInstance
        var dataPortDependencies = builder.Cache.DataPorts
            .Select(k => rulesetProvider.GetClusterDependency(k.RulesetId))
            .Union([rulesetProvider.GetSystemDataPortDependency()])
            .Distinct()
            .ToArray();

        // system dataport dependency won't be removed if it's added to the cluster as long as there's an mqtt dataport defined
        var usedDependencies = functionBlockDesignIds
            .Select(builder.DependencyResolver.ResolveFunctionBlockDesignDependency)
            .Union(dataPortDependencies)
            .ToArray();

        foreach (var unusedDependency in builder.Cache.ClusterDependencies.Except(usedDependencies))
            builder.Editors.Cluster.RemoveDependency(unusedDependency);
    }

    private static void EnsureDefaultPoolingsDependency(ClusterBuilder builder)
    {
        var poolingType = typeof(SumPooling);
        var assemblyName = poolingType.Assembly.GetName();
        var assemblyVersion = assemblyName.Version ?? throw new InvalidOperationException($"Could not determine version of {assemblyName.FullName}.");

        ClusterDependency defaultPoolings = new()
        {
            Name = assemblyName.Name!,
            Version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}",
        };

        builder.EnsureDependencyExists(defaultPoolings);
    }

    private static Task PublishCommitFailed(ConsumeContext<CommitClusterChange> context, ErrorInfo error)
    {
        var metaInfo = context.Message.MetaInfo;

        return context.Publish(new CommitClusterChangeFailed(context.Message.CorrelationId, metaInfo.ClusterId, metaInfo.ClusterVersion, error),
            context.CancellationToken);
    }

    /// <summary>
    /// called when an exception happened on consume and all retry attempts failed
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task Consume(ConsumeContext<Fault<CommitClusterChange>> context)
    {
        logger.LogCommandFaulted(
            nameof(CommitClusterChange),
            string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        var metaInfo = context.Message.Message.MetaInfo;
        var correlationId = context.CorrelationId ?? throw new InvalidOperationException("CorrelationId is missing");

        var errorInfo = ClusterEventFactory.CreateErrorInfo(ClusterError.PublishFailed, metaInfo.ClusterId, metaInfo.ClusterVersion);
        await context.Publish(new CommitClusterChangeFailed(correlationId, metaInfo.ClusterId, metaInfo.ClusterVersion, errorInfo),
            context.CancellationToken);
    }
}
