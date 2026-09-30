using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed partial class AddClusterPackageConsumer(
    IPackageStore packageStorage,
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    ILogger<AddClusterPackageConsumer> logger) :
    IConsumer<AddClusterPackage>, IConsumer<Fault<AddClusterPackage>>
{
    public async Task Consume(ConsumeContext<AddClusterPackage> context)
    {
        var command = context.Message;

        // The archive is read and deleted afterwards, so only a file the upload handler stored is accepted.
        if (!fileSystem.IsUploadedPackageFile(workspaceProvider, command.Filename))
        {
            LogPackageFileOutsideUploadDirectory(logger, command.CorrelationId, command.Filename);

            await PublishError(context, command, ClusterPackageChanged.PackageFileOutsideUploadDirectory, "The package file is not in the upload directory.");
            return;
        }

        if (!fileSystem.File.Exists(command.Filename))
        {
            LogPackageFileNotFound(logger, command.CorrelationId, command.Filename);

            await PublishError(context, command, ClusterPackageChanged.PackageFileNotFound, $"The uploaded package file '{fileSystem.Path.GetFileName(command.Filename)}' was not found.");
            return;
        }

        try
        {
            // We get a zip archive file path and try to add it to our package storage.
            var added = await packageStorage.StorePackage(command.Filename, true, context.CancellationToken);
            var changeEvent = new ClusterPackageChanged(added.Name, added.Version.ToString(), ClusterPackageChange.Add)
            {
                CorrelationId = command.CorrelationId
            };

            LogPackageStored(logger, command.CorrelationId, command.Filename);

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (PackageValidationException ex)
        {
            // The archive itself is invalid, a retry cannot succeed: report it directly instead of faulting the message.
            LogInvalidPackage(logger, ex, command.CorrelationId, command.Filename);

            TryDeleteFile(command);
            await PublishError(context, command, ex.ErrorCode, ex.Message);
            return;
        }
        catch (Exception ex)
        {
            // Possibly transient: leave the archive in place so a retry can still read it.
            // It is removed by the Fault<AddClusterPackage> consumer once all attempts have failed.
            LogError(logger, ex, command.CorrelationId, command.Filename);

            throw;
        }

        TryDeleteFile(command);
    }

    /// <summary>
    /// Called when all attempts for <see cref="AddClusterPackage"/> have failed with a non-validation error.
    /// </summary>
    public async Task Consume(ConsumeContext<Fault<AddClusterPackage>> context)
    {
        var command = context.Message.Message;

        TryDeleteFile(command);
        await PublishError(context, command, ClusterPackageChanged.AddPackageFailed, string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));
    }

    private static async Task PublishError(ConsumeContext context, AddClusterPackage command, int errorCode, string message)
    {
        var changeEvent = new ClusterPackageChanged("Unknown", "0.0.0", ClusterPackageChange.Add, new ErrorInfo(errorCode, message))
        {
            CorrelationId = command.CorrelationId
        };

        await context.Publish(changeEvent, context.CancellationToken);
    }

    private void TryDeleteFile(AddClusterPackage command)
    {
        // A Fault<AddClusterPackage> does not pass the check in Consume, so it is repeated here.
        if (!fileSystem.IsUploadedPackageFile(workspaceProvider, command.Filename))
        {
            LogPackageFileOutsideUploadDirectory(logger, command.CorrelationId, command.Filename);
            return;
        }

        try
        {
            LogDeletingPackageFile(logger, command.CorrelationId, command.Filename);

            fileSystem.File.Delete(command.Filename);
        }
        catch (Exception ex)
        {
            LogFileDeleteError(logger, ex, command.CorrelationId, command.Filename);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add cluster package archive file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogError(ILogger<AddClusterPackageConsumer> logger, Exception exception, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid cluster package archive file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogInvalidPackage(ILogger<AddClusterPackageConsumer> logger, Exception exception, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cluster package archive file '{Filename}' is not in the upload directory, ignoring it CorrelationId={CorrelationId}")]
    private static partial void LogPackageFileOutsideUploadDirectory(ILogger<AddClusterPackageConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cluster package archive file '{Filename}' not found CorrelationId={CorrelationId}")]
    private static partial void LogPackageFileNotFound(ILogger<AddClusterPackageConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting cluster package archive file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogDeletingPackageFile(ILogger<AddClusterPackageConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stored cluster package from archive file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogPackageStored(ILogger<AddClusterPackageConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete cluster package archive file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogFileDeleteError(ILogger<AddClusterPackageConsumer> logger, Exception exception, Guid correlationId, string filename);
}
