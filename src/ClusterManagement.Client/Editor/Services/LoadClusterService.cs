using System.Diagnostics;
using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Builder;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

internal sealed partial class LoadClusterService(
    IClusterService clusterService,
    IClusterEditorManagement clusterEditorManagement,
    ILogger<LoadClusterService> logger,
    IDesignProvider designProvider) : DialogServiceBase, IDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();

    private ClusterBuilder? _clusterBuilder;

    public event EventHandler<ClusterBuilder>? ClusterLoaded;

    public async Task TryLoadLatestVersion(CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await LoadClusterVersions(cancellationToken);
            if (versions.Count <= 0)
                throw new InvalidOperationException("Default cluster is not available!");

            // actually we assume to have only 1 cluster 
            var clusterModel = versions.First();

            await Load(clusterModel.Latest.Id, clusterModel.Latest.Version, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogFailure(ex, nameof(TryLoadLatestVersion));
        }
    }

    public async Task TryLoad(Guid clusterId, Version version, CancellationToken cancellationToken = default)
    {
        try
        {
            await Load(clusterId, version, cancellationToken);
        }
        catch (Exception ex)
        {
            // TODO - add feedback to the user? toast?
            if (ex.InnerException is not null)
            {
                logger.LogFailure(ex.InnerException, nameof(TryLoad));
            }

            logger.LogFailure(ex, nameof(TryLoad));
        }
        finally
        {
            // better show error in dialog if something went wrong 
            DisplayDialog = false;
        }
    }

    private async Task Load(Guid clusterId, Version version, CancellationToken cancellationToken = default)
    {
        LogQueryCluster(logger, clusterId, version);

        var cluster = await clusterService.Query(clusterId, version, cancellationToken);
        var resolver = await designProvider.CreateResolver();

        _clusterBuilder?.Dispose();
        _clusterBuilder = new ClusterBuilder(cluster, resolver);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var usedFunctionBlockDesignIds = await designProvider.GetFunctionBlockDesignIds(cluster.Dependencies);

        // ensure all designs referenced by current cluster are added
        foreach (var designId in usedFunctionBlockDesignIds)
            _clusterBuilder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(designId);

        await clusterEditorManagement.LoadDataflow(_clusterBuilder, cancellationToken);
        stopwatch.Stop();

        LogLoadingClusterDone(logger, clusterId, version, stopwatch.Elapsed);

        // maybe IDataManagementService should expose builder
        ClusterLoaded?.Invoke(this, _clusterBuilder);
    }

    /// <summary>
    /// returns a view model with cluster versions grouped by cluster.Id
    /// </summary>
    /// <returns></returns>
    private async Task<List<ClusterInfoVersionsModel>> LoadClusterVersions(CancellationToken cancellationToken = default)
    {
        var result = new List<ClusterInfoVersionsModel>();
        var clusters = await clusterService.QueryClusterInfos(null, cancellationToken);

        foreach (var group in clusters.GroupBy(c => c.Id))
        {
            var latestInfo = group.Count() <= 1 ? group.FirstOrDefault() : group.MaxBy(k => k.Version);
            if (latestInfo is null)
            {
                LogGetLatestVersionFailed(logger, group.Key);
                continue;
            }

            result.Add(new(latestInfo, [.. group.Select(k => k.Version)]));
        }

        return result;
    }

    public void Dispose()
    {
        try
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        _clusterBuilder?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Query cluster {ClusterId} version '{clusterVersion}'")]
    private static partial void LogQueryCluster(ILogger<LoadClusterService> logger, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading cluster {ClusterId} version '{ClusterVersion}' into editor took {Elapsed}")]
    private static partial void LogLoadingClusterDone(ILogger<LoadClusterService> logger, Guid clusterId, Version clusterVersion, TimeSpan elapsed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get latest version of cluster {ClusterId}")]
    private static partial void LogGetLatestVersionFailed(ILogger<LoadClusterService> logger, Guid clusterId);
}
