using System.Diagnostics;
using System.IO.Abstractions;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Public;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.EngineHosts;

internal class EngineHostLoader(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IEngineHostArtifactRepository repository,
    IOptions<ClusterManagementOptions> options,
    ILogger<EngineHostLoader> logger) : BackgroundService, IDownloader
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = workspaceProvider;
    private readonly ClusterManagementOptions _options = options.Value;
    private readonly IEngineHostArtifactRepository _repository = repository;
    private readonly ILogger<EngineHostLoader> _logger = logger;
    private readonly TaskCompletionSource _downloadProcess = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Task => _downloadProcess.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _downloadProcess.TrySetCanceled(stoppingToken));
        try
        {
            var watch = new Stopwatch();
            watch.Start();

            var result = await _repository.DownloadAndExtract(stoppingToken);

            watch.Stop();

            if (result.Error is not null)
            {
                _logger.LogError(result.Error, "Failed to download EngineHost {Version}", _options.GetEngineHostVersion());
                return;
            }

            if (result.Skipped)
            {
                _logger.LogInformation("Skip download of EngineHost {Version}", _options.GetEngineHostVersion());
                LogEngineHostVersion();
                return;
            }

            _logger.LogInformation("Downloading EngineHost {Version} took {TimeSpan}",
                _options.GetEngineHostVersion(),
                watch.Elapsed);


            LogEngineHostVersion();

            void LogEngineHostVersion()
                => _logger.LogInformation("Use EngineHost {Version} from {Location}",
                    _options.GetEngineHostVersion(),
                    _fileSystem.GetEngineHostInstallDirectory(_workspaceProvider, _options));
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
}
