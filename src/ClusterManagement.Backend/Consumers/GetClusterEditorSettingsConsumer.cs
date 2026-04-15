using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class GetClusterEditorSettingsConsumer(IClusterEditorDbContext dbContext, ILogger<GetActiveClusterConsumer> logger)
    : RequestConsumer<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>
{
    public override async Task<GetClusterEditorSettingsResponse> Respond(GetClusterEditorSettingsRequest message, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == message.UserId, cancellationToken);

        return new GetClusterEditorSettingsResponse
        {
            Settings = settings ?? new Internal.Contracts.EditorSettings { UserId = message.UserId }
        };
    }

    public override Task<GetClusterEditorSettingsResponse> HandleException(GetClusterEditorSettingsRequest message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogHandleException(e, nameof(GetClusterEditorSettingsRequest));

        return Task.FromResult(new GetClusterEditorSettingsResponse
        {
            RequestError = new ErrorInfo(ClusterError.EditorLoadSettingsFailed, $"Failed to load settings for user {message.UserId}"),
        });
    }
}
