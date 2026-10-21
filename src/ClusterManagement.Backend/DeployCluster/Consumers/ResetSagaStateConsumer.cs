using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Extensions;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed partial class ResetSagaStateConsumer(IClusterSagaDbContext sagaDbContext, IClusterLock clusterLock, ILogger<ResetSagaStateConsumer> logger) :
    IConsumer<ResetSagaState>, IConsumer<Fault<ResetSagaState>>
{
    public async Task Consume(ConsumeContext<ResetSagaState> context)
    {
        var command = context.Message;

        // Earlier versions could leave several deployments of one cluster behind; all of them are reset.
        var deployStates = await sagaDbContext.DeployClusterState
            .Where(k => k.ClusterId == command.ClusterId)
            .ToListAsync(context.CancellationToken);

        if (deployStates.Count == 0)
        {
            LogDeployState(logger, command.ClusterId);
        }
        else
        {
            LogReset(logger, command.ClusterId, deployStates.Count, command.RequestedBy);
            foreach (var deployState in deployStates)
                TryLogClusterState(deployState);

            sagaDbContext.DeployClusterState.RemoveRange(deployStates);
            await sagaDbContext.SaveChangesAsync(context.CancellationToken);

            // A removed deployment never releases the commit version it reserved; a stored one is no longer reserved
            foreach (var deployState in deployStates.Where(s => !s.IsCommitVersionEmpty()))
                clusterLock.Release(deployState.ClusterId, deployState.CommitVersion);
        }

        // Whoever waits for the end of a removed deployment would wait forever, so each one ends as failed.
        foreach (var deployState in deployStates)
            await context.Publish(Failed(deployState), context.CancellationToken);

        await context.Publish(new DeploymentStateReset(command.CorrelationId, command.ClusterId)
        {
            Reset = [.. deployStates.Select(ToDeploymentState)],
        }, context.CancellationToken);
    }

    /// <summary>
    /// Called when all attempts for <see cref="ResetSagaState"/> have failed, so the user learns why.
    /// </summary>
    public async Task Consume(ConsumeContext<Fault<ResetSagaState>> context)
    {
        var command = context.Message.Message;
        await context.Publish(new DeploymentStateReset(command.CorrelationId, command.ClusterId)
        {
            Error = new(ClusterError.UnknownError, string.Join(" | ", context.Message.Exceptions.Select(e => e.Message))),
        }, context.CancellationToken);
    }

    private static ClusterUpdateFailed Failed(DeployClusterState deployState)
        => new(deployState.CorrelationId, deployState.ClusterId, VersionOf(deployState))
        {
            Duration = DateTime.UtcNow - deployState.Started,
            State = DeployClusterStateMachine.GetState(deployState.CurrentState),
            Reason = ResetSagaState.FailureReason,
            WasReset = true,
        };

    private static DeploymentState ToDeploymentState(DeployClusterState deployState)
        => new(deployState.ClusterId,
            DeployClusterStateMachine.GetState(deployState.CurrentState),
            deployState.ActiveVersion,
            VersionOf(deployState))
        {
            CorrelationId = deployState.CorrelationId,
        };

    // The saga fails a deployment with the version it committed, or before that with the one it targets.
    private static Version VersionOf(DeployClusterState deployState)
        => deployState.IsCommitVersionEmpty() ? deployState.TargetVersion : deployState.CommitVersion;

    private void TryLogClusterState(DeployClusterState deployState)
    {
        try
        {
            LogResetClusterState(logger, JsonSerializer.Serialize(deployState, GetSerializerOptions()));
        }
        catch (Exception ex)
        {
            LogClusterStateError(logger, ex);
        }
    }

    private static JsonSerializerOptions GetSerializerOptions()
        => new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { DefaultJsonSerializerSettings.CreateDateTimeConverter() },
            WriteIndented = true
        };


    [LoggerMessage(Level = LogLevel.Information, Message = "No deploy state found for cluster {ClusterId}, nothing to reset")]
    private static partial void LogDeployState(ILogger<ResetSagaStateConsumer> logger, Guid clusterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resetting {Count} deployment states of cluster {ClusterId} for {User}")]
    private static partial void LogReset(ILogger<ResetSagaStateConsumer> logger, Guid clusterId, int count, string user);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reset current deployment state: {State}")]
    private static partial void LogResetClusterState(ILogger<ResetSagaStateConsumer> logger, string state);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to log serialized deployment state")]
    private static partial void LogClusterStateError(ILogger<ResetSagaStateConsumer> logger, Exception exception);
}
