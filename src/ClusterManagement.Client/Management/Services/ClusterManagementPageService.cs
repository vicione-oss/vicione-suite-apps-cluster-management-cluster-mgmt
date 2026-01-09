using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Utils;

namespace ClusterManagement.Client.Management.Services;

public sealed class ClusterManagementPageService : IClusterManagementService,
    IEventConsumer<ClusterUpdateStarted>,
    IEventConsumer<ClusterUpdateChanged>,
    IEventConsumer<ClusterUpdateCompleted>,
    IEventConsumer<ClusterUpdateFailed>,
    IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly IClusterService _clusterService;
    private readonly IJsInterop _jsInterop;
    private readonly ILogger<ClusterManagementPageService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private Guid? CorrelationId { get; set; }

    public event EventHandler<ClusterUpdateChanged>? OnClusterUpdateChanged;
    public event EventHandler<ClusterUpdateCompleted>? OnClusterUpdateCompleted;
    public event EventHandler<ClusterUpdateFailed>? OnClusterUpdateFailed;
    public event EventHandler<ClusterUpdateStarted>? OnClusterUpdateStarted;

    public ClusterManagementPageService(IUiMediator mediator, IClusterService clusterService, IJsInterop jsInterop, ILogger<ClusterManagementPageService> logger)
    {
        _mediator = mediator;
        _clusterService = clusterService;
        _jsInterop = jsInterop;
        _logger = logger;

        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateStarted>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateChanged>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateCompleted>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateFailed>(this));
    }

    public void Dispose()
        => _subscriptionHandle.Dispose();

    public async Task DeployCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default)
    {
        var command = new DeployCluster(clusterId, version);
        CorrelationId = command.CorrelationId;

        _logger.LogDebug("Sending {Command} clusterId:{ClusterId} version:{Version} -> correlationId:{CorrelationId}",
            nameof(Public.Commands.DeployCluster),
            clusterId,
            version,
            CorrelationId);

        await _mediator.Send(command, cancellationToken);
    }

    public async Task<List<ClusterInfoModel>> QueryClusterVersions(Guid? clusterId = null, CancellationToken cancellationToken = default)
    {
        var infos = (await _clusterService.QueryClusterInfos(clusterId, cancellationToken))
            .Select(info => new ClusterInfoModel(info));

        var clusters = new List<ClusterInfoModel>(infos);
        var latest = clusters.GroupBy(c => c.Id);
        foreach (var group in latest)
        {
            var latestVersion = group.MaxBy(c => c.Version);
            if (latestVersion is not null)
                latestVersion.IsLatest = true;
        }

        return clusters;
    }

    public Task RemoveCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default)
        => _clusterService.Remove(clusterId, version, cancellationToken);

    public Task Consume(ClientContext<ClusterUpdateStarted> context, CancellationToken cancellationToken)
    {
        if (CorrelationId == context.CorrelationId)
        {
            _logger.LogDebug("Received {Name} event triggered by us", context.CorrelationId);
        }
        else
        {
            _logger.LogDebug("Received {Name} event not started by us", context.CorrelationId);
        }

        OnClusterUpdateStarted?.Invoke(this, context.Message);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateChanged> context, CancellationToken cancellationToken)
    {
        if (CorrelationId == context.CorrelationId)
        {
            _logger.LogDebug("Received {Name} event triggered by us", context.CorrelationId);
        }
        else
        {
            _logger.LogDebug("Received {Name} event not started by us", context.CorrelationId);
        }

        OnClusterUpdateChanged?.Invoke(this, context.Message);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateCompleted> context, CancellationToken cancellationToken)
    {
        if (CorrelationId == context.CorrelationId)
        {
            _logger.LogDebug("Received {Name} event triggered by us", context.CorrelationId);
        }
        else
        {
            _logger.LogDebug("Received {Name} event not started by us", context.CorrelationId);
        }

        OnClusterUpdateCompleted?.Invoke(this, context.Message);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateFailed> context, CancellationToken cancellationToken)
    {
        if (CorrelationId == context.CorrelationId)
        {
            _logger.LogDebug("Received {Name} event triggered by us", context.CorrelationId);
        }
        else
        {
            _logger.LogDebug("Received {Name} event not started by us", context.CorrelationId);
        }

        OnClusterUpdateFailed?.Invoke(this, context.Message);

        return Task.CompletedTask;
    }

    public async Task DownloadClusterJson(Guid clusterId, Version version, CancellationToken cancellationToken = default)
    {
        try
        {
            var cluster = await _clusterService.Query(clusterId, version, cancellationToken) ??
                throw new InvalidOperationException($"Failed to query cluster {clusterId} v{version}");

            await _jsInterop.DownloadClusterJson(cluster);
        }
        catch (Exception e)
        {
            _logger.LogError(e, nameof(DownloadClusterJson));
        }
    }
}
