using System.Diagnostics;
using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Builder;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

internal sealed class LoadClusterService : DialogServiceBase, IDisposable
{
    private readonly IClusterService _clusterService;
    private readonly IDataManagementService _dataManagementService;
    private readonly ILogger<LoadClusterService> _logger;
    private readonly IDesignProvider _designProvider;
    private ClusterBuilder? _clusterBuilder;

    public event EventHandler<ClusterBuilder>? ClusterLoaded;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "this notation is clearer")]
    public LoadClusterService(IClusterService clusterService,
        IDataManagementService dataManagementService,
        ILogger<LoadClusterService> logger,
        IDesignProvider designProvider)
    {
        _clusterService = clusterService;
        _dataManagementService = dataManagementService;
        _logger = logger;
        _designProvider = designProvider;
    }

    public async Task TryLoadLatestVersion()
    {
        try
        {
            var versions = await LoadClusterVersions();
            if (versions.Count <= 0)
                throw new InvalidOperationException("Default cluster is not available!");

            // actually we assume to have only 1 cluster 
            var clusterModel = versions.First();

            await Load(clusterModel.Latest.Id, clusterModel.Latest.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(TryLoadLatestVersion));
        }
    }

    public async Task TryLoad(Guid clusterId, Version version)
    {
        try
        {
            await Load(clusterId, version);
        }
        catch (Exception ex)
        {
            // TODO - add feedback to the user? toast?
            if (ex.InnerException is not null)
            {
                _logger.LogCritical(ex.InnerException, nameof(TryLoad));
            }

            _logger.LogError(ex, nameof(TryLoad));
        }
        finally
        {
            // better show error in dialog if something went wrong 
            DisplayDialog = false;
        }
    }

    private async Task Load(Guid clusterId, Version version)
    {
        var cluster = await _clusterService.Query(clusterId, version);
        var resolver = await _designProvider.CreateResolver();

        _clusterBuilder?.Dispose();
        _clusterBuilder = new ClusterBuilder(cluster, resolver);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var usedFunctionBlockDesignIds = await _designProvider.GetFunctionBlockDesignIds(cluster.Dependencies);

        // ensure all designs referenced by current cluster are added
        foreach (var designId in usedFunctionBlockDesignIds)
            _clusterBuilder.Editors.FunctionBlockDesign.AddFunctionBlockDesign(designId);

        _dataManagementService.LoadDataflow(_clusterBuilder);
        stopwatch.Stop();

        _logger.LogInformation("Loading cluster {Id} v{Version} into editor took {Elapsed}", clusterId, version, stopwatch.Elapsed);

        // maybe IDataManagementService should expose builder
        ClusterLoaded?.Invoke(this, _clusterBuilder);
    }

    /// <summary>
    /// returns a view model with cluster versions grouped by cluster.Id
    /// </summary>
    /// <returns></returns>
    private async Task<List<ClusterInfoVersionsModel>> LoadClusterVersions()
    {
        var result = new List<ClusterInfoVersionsModel>();
        var clusters = await _clusterService.QueryClusterInfos();

        foreach (var group in clusters.GroupBy(c => c.Id))
        {
            var latestInfo = group.Count() <= 1 ? group.FirstOrDefault() : group.MaxBy(k => k.Version);
            if (latestInfo is null)
            {
                _logger.LogWarning("Failed to get latest version of cluster {Key}", group.Key);
                continue;
            }

            result.Add(new(latestInfo, [.. group.Select(k => k.Version)]));
        }

        return result;
    }

    public void Dispose()
        => _clusterBuilder?.Dispose();
}
