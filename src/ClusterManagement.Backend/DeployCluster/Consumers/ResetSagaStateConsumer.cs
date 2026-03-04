using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Iodds.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed class ResetSagaStateConsumer(IClusterSagaDbContext sagaDbContext, ILogger<ResetSagaStateConsumer> logger) : IConsumer<ResetSagaState>
{
    public async Task Consume(ConsumeContext<ResetSagaState> context)
    {
        var deployState = await sagaDbContext.DeployClusterState
                .FirstOrDefaultAsync(k => k.ClusterId == context.Message.ClusterId, context.CancellationToken);

        if (deployState is null)
        {
            logger.LogInformation("No deploy state found for cluster {ClusterId}, nothing to reset", context.Message.ClusterId);
            return;
        }

        TryLogClusterState(deployState);

        sagaDbContext.DeployClusterState.Remove(deployState);
        await sagaDbContext.SaveChangesAsync(context.CancellationToken);
    }

    private void TryLogClusterState(DeployClusterState deployState)
    {
        try
        {
            logger.LogWarning("Reset current deployment state: {State}", JsonSerializer.Serialize(deployState, GetSerializerOptions()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log serialized deployment state");
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
}
