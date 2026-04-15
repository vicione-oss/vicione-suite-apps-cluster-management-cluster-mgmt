using ClusterManagement.Client.Management.Logging;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;
using ViciOne.Cluster.Model;

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
    private readonly ILogger<ClusterManagementPageService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private Guid? CorrelationId { get; set; }

    public event EventHandler<ClusterUpdateChanged>? OnClusterUpdateChanged;
    public event EventHandler<ClusterUpdateCompleted>? OnClusterUpdateCompleted;
    public event EventHandler<ClusterUpdateFailed>? OnClusterUpdateFailed;
    public event EventHandler<ClusterUpdateStarted>? OnClusterUpdateStarted;

    public ClusterManagementPageService(IUiMediator mediator, IClusterService clusterService, ILogger<ClusterManagementPageService> logger)
    {
        _mediator = mediator;
        _clusterService = clusterService;
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

        _logger.LogDeployCluster(clusterId, version, CorrelationId);

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
            latestVersion?.IsLatest = true;
        }

        return clusters;
    }

    public Task RemoveCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default)
        => _clusterService.Remove(clusterId, version, cancellationToken);

    public Task Consume(ClientContext<ClusterUpdateStarted> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterUpdateStarted), context.CorrelationId);
        OnClusterUpdateStarted?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateChanged> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterUpdateChanged), context.CorrelationId);
        OnClusterUpdateChanged?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateCompleted> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterUpdateCompleted), context.CorrelationId);
        OnClusterUpdateCompleted?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterUpdateFailed> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterUpdateFailed), context.CorrelationId);
        OnClusterUpdateFailed?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    private void LogReceivedEvent(string eventName, Guid? correlationId)
    {
        if (CorrelationId == correlationId)
            _logger.LogReceivedOwnEvent(eventName, correlationId);
        else
            _logger.LogReceivedExternalEvent(eventName, correlationId);
    }

    public Task<Cluster> QueryCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default)
        => _clusterService.Query(clusterId, version, cancellationToken)
        ?? throw new InvalidOperationException($"Failed to query cluster {clusterId} v{version}");

    public Task ResetSagaState(Guid clusterId, CancellationToken token)
        => _mediator.Send(new ResetSagaState(clusterId), token);

    public async Task<DeploymentState?> QueryClusterDeploymentState(Guid clusterId, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Request<GetDeployClusterStateRequest, GetDeployClusterStateResponse>(new(clusterId), cancellationToken);
        if (response.RequestError is not null)
        {
            _logger.LogQueryClusterDeploymentStateError(clusterId, response.RequestError);
            return null;
        }

        return response.State;
    }
}
