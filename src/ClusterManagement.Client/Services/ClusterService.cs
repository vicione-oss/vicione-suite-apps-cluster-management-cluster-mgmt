using System.Diagnostics;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Services;

internal sealed class ClusterService(IUiMediator mediator,
    ILogger<ClusterService> logger) : IClusterService
{
    private readonly IUiMediator _mediator = mediator;
    private readonly ILogger<ClusterService> _logger = logger;

    public async Task<List<Version>> QueryVersions(Guid clusterId)
        => [.. (await QueryClusterInfos(clusterId)).Select(k => k.Version)];

    public async Task<Dictionary<Guid, List<Version>>> QueryVersions()
        => (await QueryClusterInfos())
            .GroupBy(n => n.Id, n => n.Version)
            .ToDictionary(g => g.Key, g => g.ToList());

    public async Task<Version?> QueryLatestVersion(Guid clusterId)
        => (await QueryVersions(clusterId)).Max(k => k);

    public async Task<List<ClusterInfo>> QueryClusterInfos(Guid? clusterId = null)
    {
        _logger.LogDebug("Sending mediator query {Name}", nameof(QueryClusterInfos));

        var query = new GetClusterInfosRequest
        {
            ClusterId = clusterId
        };
        var response = await _mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(query);

        _logger.LogDebug("Result mediator query {Name} contains {Count} items",
            nameof(QueryClusterInfos),
            response.Results.Count);

        return response.Results;
    }

    public async Task<Cluster> Query(Guid clusterId, Version? version)
    {
        _logger.LogDebug("Sending mediator query {Name} width id {Id}",
            nameof(Query),
            clusterId);

        var response = await _mediator
            .Request<GetClusterRequest, GetClusterResponse>(new GetClusterRequest(clusterId, version));

        if (response.RequestError != null)
            throw new InvalidOperationException(response.RequestError.Message);

        if (response.CompressedCluster is null || response.CompressedCluster.Length == 0)
            throw new InvalidOperationException($"Received empty data for cluster {clusterId} v{version}");

        var result = await ClusterSerializer.Decompress(response.CompressedCluster);
        _logger.LogDebug("Result mediator query {Name} returned item id {Id}",
            nameof(Query),
            result.Id);

        return result;
    }

    public async Task CommitChange(Cluster cluster, CommitClusterOptions? options)
    {
        var stopwatch = new Stopwatch();
        _logger.LogDebug("Publishing {Name} width id {Id}, version {Version}",
            nameof(CommitClusterChange),
            cluster.Id,
            cluster.Version);

        stopwatch.Restart();
        var compressedCluster = ClusterSerializer.Compress(cluster);
        stopwatch.Stop();
        _logger.LogInformation("Serializing {Cluster} took {Elapsed}", nameof(Cluster), stopwatch.Elapsed);

        stopwatch.Restart();
        var command = new CommitClusterChange(compressedCluster, new CommitClusterMetaInfo(cluster), options);
        await _mediator.Send(command);
        stopwatch.Stop();

        _logger.LogInformation("Sending {Command} command took {Elapsed}", nameof(CommitClusterChange), stopwatch.Elapsed);
    }

    public Task Remove(Guid clusterId, Version version)
    {
        var command = new RemoveCluster(clusterId, version);
        return _mediator.Send(command);
    }
}
