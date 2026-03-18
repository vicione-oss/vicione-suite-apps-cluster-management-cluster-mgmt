using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class AddOrUpdateClusterEditorSettingsConsumer(IClusterEditorDbContext dbContext, ILogger<GetActiveClusterConsumer> logger)
    : IConsumer<AddOrUpdateClusterEditorSettings>, IConsumer<Fault<AddOrUpdateClusterEditorSettings>>
{
    public async Task Consume(ConsumeContext<AddOrUpdateClusterEditorSettings> context)
    {
        LogConsume(logger, nameof(AddOrUpdateClusterEditorSettings), context.Message.CorrelationId, context.Message.Settings.UserId);

        var result = await AddOrUpdateSettings(context.Message.Settings, context.CancellationToken);
        if (result is null)
            throw new InvalidOperationException("Failed to create or update editor settings");

        var changeEvent = new ClusterEditorSettingsChanged(result.Settings, result.CrudAction) { CorrelationId = context.Message.CorrelationId };

        await context.Publish(changeEvent, context.CancellationToken);
    }

    private async Task<AddOrUpdateClusterEditorSettingsResult?> AddOrUpdateSettings(EditorSettings settings, CancellationToken cancellationToken)
    {
        var existingSettings = dbContext.Settings.FirstOrDefault(s => s.UserId == settings.UserId);
        if (existingSettings is null)
        {
            var settingsEntry = dbContext.Settings.Add(settings);

            if (await dbContext.SaveChangesAsync(cancellationToken) == 0)
                return null;

            // only publish if we really changed the db 
            return new AddOrUpdateClusterEditorSettingsResult
            {
                CrudAction = CrudAction.Created,
                Settings = settingsEntry.Entity
            };
        }

        existingSettings.GridMode = settings.GridMode;
        existingSettings.MinimapNodeColoring = settings.MinimapNodeColoring;
        existingSettings.NodeAlignmentBorder = settings.NodeAlignmentBorder;
        existingSettings.PanBehavior = settings.PanBehavior;
        existingSettings.SimplifiedView = settings.SimplifiedView;
        dbContext.Settings.Update(existingSettings);

        if (await dbContext.SaveChangesAsync(cancellationToken) <= 0)
            return null;    // could happen on queued events

        return new AddOrUpdateClusterEditorSettingsResult
        {
            CrudAction = CrudAction.Updated,
            Settings = existingSettings
        };
    }

    public async Task Consume(ConsumeContext<Fault<AddOrUpdateClusterEditorSettings>> context)
    {
        LogFaulted(logger, nameof(AddOrUpdateClusterEditorSettings), string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        var origMessage = context.Message.Message;
        var errorInfo = new ErrorInfo(ClusterError.EditorStoreSettingsFailed, $"Failed to add or update settings for user {origMessage.Settings.UserId}");
        var changeEvent = new ClusterEditorSettingsChanged(origMessage.Settings, CrudAction.Created) { CorrelationId = origMessage.CorrelationId };

        await context.Publish(changeEvent);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume {Command} CorrelationId:{CorrelationId} for UserId:{UserId}")]
    private static partial void LogConsume(ILogger logger, string command, Guid correlationId, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Command} faulted with errors: {Errors}")]
    private static partial void LogFaulted(ILogger logger, string command, string errors);

    private sealed record AddOrUpdateClusterEditorSettingsResult
    {
        public CrudAction CrudAction { get; init; }
        public required EditorSettings Settings { get; init; }
    }
}
