using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class GetClusterEditorSettingsConsumer(IClusterEditorDbContext dbContext, ILogger<GetActiveClusterConsumer> logger)
    : RequestConsumer<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>
{
    protected override async Task<GetClusterEditorSettingsResponse> Respond(ConsumeContext<GetClusterEditorSettingsRequest> context)
    {
        LogConsume(logger, nameof(GetClusterEditorSettingsRequest), context.CorrelationId, context.Message.UserId);

        var settings = await dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == context.Message.UserId, context.CancellationToken);

        return new GetClusterEditorSettingsResponse
        {
            Settings = settings ?? new Internal.Contracts.EditorSettings { UserId = context.Message.UserId }
        };
    }

    protected override Task<GetClusterEditorSettingsResponse> HandleException(ConsumeContext<GetClusterEditorSettingsRequest> context, Exception e)
    {
        LogHandleException(logger, e, nameof(GetClusterEditorSettingsRequest));

        return Task.FromResult(new GetClusterEditorSettingsResponse
        {
            RequestError = new ErrorInfo(ClusterError.EditorLoadSettingsFailed, $"Failed to load settings for user {context.Message.UserId}"),
        });
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume {Request} CorrelationId:{CorrelationId} UserId:{UserId}")]
    private static partial void LogConsume(ILogger logger, string request, Guid? correlationId, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle {Request}")]
    private static partial void LogHandleException(ILogger logger, Exception exception, string request);
}
