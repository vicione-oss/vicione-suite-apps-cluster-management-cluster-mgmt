using System.Diagnostics;
using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Services;

internal sealed class ClusterService :
    CompletionSourceHandlerBase<IClusterServiceResult>,
    IClusterService,
    IEventConsumer<ClusterChanged>,
    IEventConsumer<CommitClusterChangeFailed>
{
    private readonly ILogger<ClusterService> _logger;

    public event Func<ClusterChanged, CancellationToken, Task>? ClusterChanged;

    public event Func<CommitClusterChangeFailed, CancellationToken, Task>? CommitClusterChangeFailed;

    public ClusterService(IUiMediator mediator, ILogger<ClusterService> logger) : base(mediator)
    {
        Register<ClusterChanged>();
        Register<CommitClusterChangeFailed>();
        _logger = logger;
    }

    public async Task<List<Version>> QueryVersions(Guid clusterId, CancellationToken cancellationToken = default)
        => [.. (await QueryClusterInfos(clusterId, cancellationToken)).Select(k => k.Version)];

    public async Task<Dictionary<Guid, List<Version>>> QueryVersions(CancellationToken cancellationToken = default)
        => (await QueryClusterInfos(null, cancellationToken))
            .GroupBy(n => n.Id, n => n.Version)
            .ToDictionary(g => g.Key, g => g.ToList());

    public async Task<Version?> QueryLatestVersion(Guid clusterId, CancellationToken cancellationToken = default)
        => (await QueryVersions(clusterId, cancellationToken)).Max(k => k);

    public async Task<List<ClusterInfo>> QueryClusterInfos(Guid? clusterId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogQueryClusterInfos();

        var query = new GetClusterInfosRequest
        {
            ClusterId = clusterId
        };
        var response = await Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(query, cancellationToken);

        _logger.LogQueryClusterInfosResult(response.Results.Count);

        return response.Results;
    }

    public async Task<Cluster> Query(Guid clusterId, Version? version, CancellationToken cancellationToken = default)
    {
        _logger.LogQueryCluster(clusterId);

        var response = await Mediator
            .Request<GetClusterRequest, GetClusterResponse>(new GetClusterRequest(clusterId, version), cancellationToken);

        if (response.RequestError != null)
            throw new InvalidOperationException(response.RequestError.Message);

        if (response.CompressedCluster is null || response.CompressedCluster.Length == 0)
            throw new InvalidOperationException($"Received empty data for cluster {clusterId} v{version}");

        var result = await ClusterSerializer.Decompress(response.CompressedCluster, cancellationToken: cancellationToken);
        _logger.LogQueryClusterResult(result.Id);

        return result;
    }

    public async Task<IClusterServiceResult> CommitChange(Cluster cluster, CommitClusterOptions? options, CancellationToken cancellationToken = default)
    {
        var stopwatch = new Stopwatch();
        _logger.LogCommitClusterChange(cluster.Id, cluster.Version);

        stopwatch.Restart();
        var compressedCluster = ClusterSerializer.Compress(cluster);
        stopwatch.Stop();
        _logger.LogElapsedTime("Serializing cluster", stopwatch.Elapsed);

        stopwatch.Restart();
        try
        {
            var command = new CommitClusterChange(compressedCluster, new CommitClusterMetaInfo(cluster), options);
            return await SendAndWaitForCompletion(command, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogElapsedTime($"Sending {nameof(CommitClusterChange)} command", stopwatch.Elapsed);
        }
    }

    public async Task<IClusterServiceResult> Remove(Guid clusterId, Version version, CancellationToken cancellationToken = default)
    {
        var command = new RemoveCluster(clusterId, version);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    protected override IClusterServiceResult CreateSuccessResult()
        => new ClusterServiceSuccessResult();

    protected override IClusterServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new ClusterServiceErrorResult(errorMessage, errorCode);

    public async Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
    {
        if (CompleteWithSuccess(context.Message.CorrelationId))
        {
            await NotifyClusterChanged(context.Message, cancellationToken);
        }
    }

    public async Task Consume(ClientContext<CommitClusterChangeFailed> context, CancellationToken cancellationToken)
    {
        if (CompleteWithError(context.Message.CorrelationId, context.Message.Error))
        {
            await NotifyCommitClusterChangeFailed(context.Message, cancellationToken);
        }
    }

    private async Task NotifyClusterChanged(ClusterChanged change, CancellationToken cancellationToken)
    {
        if (ClusterChanged is not null)
            await ClusterChanged.Invoke(change, cancellationToken);
    }

    private async Task NotifyCommitClusterChangeFailed(CommitClusterChangeFailed change, CancellationToken cancellationToken)
    {
        if (CommitClusterChangeFailed is not null)
            await CommitClusterChangeFailed.Invoke(change, cancellationToken);
    }
}
