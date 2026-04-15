using System.Diagnostics;
using System.IO.Abstractions;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

internal partial class EngineHostLoader(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IEngineHostArtifactRepository repository,
    IOptions<ClusterManagementOptions> options,
    ILogger<EngineHostLoader> logger) : BackgroundService, IDownloader
{
    private readonly ClusterManagementOptions _options = options.Value;
    private readonly TaskCompletionSource _downloadProcess = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Task => _downloadProcess.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _downloadProcess.TrySetCanceled(stoppingToken));
        try
        {
            var watch = new Stopwatch();
            watch.Start();

            var result = await repository.DownloadAndExtract(stoppingToken);

            watch.Stop();

            var engineHostVersion = _options.GetEngineHostVersion();

            if (result.Error is not null)
            {
                LogProcessDirectoryError(logger, result.Error, engineHostVersion);
                return;
            }

            LogEngineHostVersion(logger, engineHostVersion, fileSystem.GetEngineHostInstallDirectory(workspaceProvider, _options));

            if (result.Skipped)
            {
                LogSkippedDownload(logger, engineHostVersion);
                return;
            }

            LogSuccess(logger, engineHostVersion, watch.Elapsed);

        }
        catch (Exception ex)
        {
            _downloadProcess.TrySetException(ex);
        }
        finally
        {
            _downloadProcess.TrySetResult();
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to download EngineHost {Version}")]
    private static partial void LogProcessDirectoryError(ILogger<EngineHostLoader> logger, Exception exception, SemVersion version);

    [LoggerMessage(LogLevel.Information, "Use EngineHost {Version} from {Location}")]
    private static partial void LogEngineHostVersion(ILogger<EngineHostLoader> logger, SemVersion version, string location);

    [LoggerMessage(LogLevel.Information, "Skip download of EngineHost {Version}")]
    private static partial void LogSkippedDownload(ILogger<EngineHostLoader> logger, SemVersion version);

    [LoggerMessage(LogLevel.Information, "Downloading EngineHost {Version} took {Elapsed}")]
    private static partial void LogSuccess(ILogger<EngineHostLoader> logger, SemVersion version, TimeSpan elapsed);
}
