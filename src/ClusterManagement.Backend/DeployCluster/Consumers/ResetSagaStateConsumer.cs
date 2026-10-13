using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Internal.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed partial class ResetSagaStateConsumer(IClusterSagaDbContext sagaDbContext, ILogger<ResetSagaStateConsumer> logger) : IConsumer<ResetSagaState>
{
    public async Task Consume(ConsumeContext<ResetSagaState> context)
    {
        // Earlier versions could leave several deployments of one cluster behind; all of them are reset.
        var deployStates = await sagaDbContext.DeployClusterState
                .Where(k => k.ClusterId == context.Message.ClusterId)
                .ToListAsync(context.CancellationToken);

        if (deployStates.Count == 0)
        {
            LogDeployState(logger, context.Message.ClusterId);
            return;
        }

        foreach (var deployState in deployStates)
            TryLogClusterState(deployState);

        sagaDbContext.DeployClusterState.RemoveRange(deployStates);
        await sagaDbContext.SaveChangesAsync(context.CancellationToken);
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reset current deployment state: {State}")]
    private static partial void LogResetClusterState(ILogger<ResetSagaStateConsumer> logger, string state);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to log serialized deployment state")]
    private static partial void LogClusterStateError(ILogger<ResetSagaStateConsumer> logger, Exception exception);
}
