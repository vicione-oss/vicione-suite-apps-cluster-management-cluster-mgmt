using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class AddOrUpdateClusterEditorSettingsConsumer(IClusterEditorDbContext dbContext, ILogger<GetActiveClusterConsumer> logger)
    : IConsumer<AddOrUpdateClusterEditorSettings>, IConsumer<Fault<AddOrUpdateClusterEditorSettings>>
{
    public async Task Consume(ConsumeContext<AddOrUpdateClusterEditorSettings> context)
    {
        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} for UserId:{UserId}",
            nameof(AddOrUpdateClusterEditorSettings), context.CorrelationId, context.Message.Settings.UserId);

        try
        {
            var result = await AddOrUpdateSettings(context.Message.Settings, context.CancellationToken);
            if (result is not null)
                await context.Publish(new ClusterEditorSettingsChanged(result.Settings, result.CrudAction), context.CancellationToken);
        }
        catch (DbUpdateException e)
        {
            logger.LogWarning(e, "Consume {Command} CorrelationId:{CorrelationId} for UserId:{UserId} Retry:{Times}",
                nameof(AddOrUpdateClusterEditorSettings), context.CorrelationId, context.Message.Settings.UserId, context.GetRetryCount());

            throw;
        }
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
        logger.LogError("{Command} faulted with errors: {Errors}",
            nameof(AddOrUpdateClusterEditorSettings),
            string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        await context.Publish(new ErrorInfo(ClusterError.EditorStoreSettingsFailed, $"Failed to add or update settings for user {context.Message.Message.Settings.UserId}"));
    }

    private sealed record AddOrUpdateClusterEditorSettingsResult
    {
        public CrudAction CrudAction { get; init; }
        public required EditorSettings Settings { get; init; }
    }
}
