using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterEditorSettingsConsumer(IClusterEditorDbContext dbContext, ILogger<GetActiveClusterConsumer> logger)
    : RequestConsumer<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>
{
    protected override async Task<GetClusterEditorSettingsResponse> Respond(ConsumeContext<GetClusterEditorSettingsRequest> context)
    {
        logger.LogDebug("Consume {Request} CorrelationId:{CorrelationId} UserId:{Id} ",
            nameof(GetClusterEditorSettingsRequest),
            context.CorrelationId,
            context.Message.UserId);

        var settings = await dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == context.Message.UserId, context.CancellationToken);

        var requestError = settings is null
            ? new ErrorInfo(ClusterError.EditorLoadSettingsFailed, $"Failed to load settings for user {context.Message.UserId}")
            : null;

        return new GetClusterEditorSettingsResponse
        {
            Settings = settings,
            RequestError = requestError
        };
    }

    protected override Task<GetClusterEditorSettingsResponse> HandleException(ConsumeContext<GetClusterEditorSettingsRequest> context, Exception e)
    {
        logger.LogError(e, "Failed to handle {Request}", nameof(GetClusterEditorSettingsRequest));

        return Task.FromResult(new GetClusterEditorSettingsResponse
        {
            RequestError = new ErrorInfo(ClusterError.EditorLoadSettingsFailed, $"Failed to load settings for user {context.Message.UserId}"),
        });
    }
}
