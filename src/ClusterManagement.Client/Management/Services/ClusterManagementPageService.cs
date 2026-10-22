using ClusterManagement.Client.Management.Logging;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
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
    IEventConsumer<ClusterUpdateRejected>,
    IEventConsumer<ClusterNodeDeploymentStepChanged>,
    IEventConsumer<ClusterNodeDeploymentPlanned>,
    IEventConsumer<DeploymentStateReset>,
    IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly IClusterService _clusterService;
    private readonly ILogger<ClusterManagementPageService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private Guid? CorrelationId { get; set; }
    private Guid? _resetCorrelationId;

    public event EventHandler<ClusterUpdateChanged>? OnClusterUpdateChanged;
    public event EventHandler<ClusterUpdateCompleted>? OnClusterUpdateCompleted;
    public event EventHandler<ClusterUpdateFailed>? OnClusterUpdateFailed;
    public event EventHandler<ClusterUpdateRejected>? OnClusterUpdateRejected;
    public event EventHandler<ClusterUpdateStarted>? OnClusterUpdateStarted;
    public event EventHandler<ClusterNodeDeploymentStepChanged>? OnClusterNodeDeploymentStepChanged;
    public event EventHandler<ClusterNodeDeploymentPlanned>? OnClusterNodeDeploymentPlanned;
    public event EventHandler<DeploymentStateReset>? OnDeploymentStateReset;

    public ClusterManagementPageService(IUiMediator mediator, IClusterService clusterService, ILogger<ClusterManagementPageService> logger)
    {
        _mediator = mediator;
        _clusterService = clusterService;
        _logger = logger;

        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateStarted>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateChanged>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateCompleted>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateFailed>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterUpdateRejected>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterNodeDeploymentStepChanged>(this));
        _subscriptionHandle.Add(_mediator.Register<ClusterNodeDeploymentPlanned>(this));
        _subscriptionHandle.Add(_mediator.Register<DeploymentStateReset>(this));
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

    // The rejection carries the correlation id of the rejected command, so only the session that sent it is told.
    public Task Consume(ClientContext<ClusterUpdateRejected> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterUpdateRejected), context.CorrelationId);
        if (context.Message.CorrelationId == CorrelationId)
            OnClusterUpdateRejected?.Invoke(this, context.Message);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterNodeDeploymentStepChanged> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterNodeDeploymentStepChanged), context.CorrelationId);
        OnClusterNodeDeploymentStepChanged?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<ClusterNodeDeploymentPlanned> context, CancellationToken cancellationToken)
    {
        LogReceivedEvent(nameof(ClusterNodeDeploymentPlanned), context.CorrelationId);
        OnClusterNodeDeploymentPlanned?.Invoke(this, context.Message);
        return Task.CompletedTask;
    }

    // Every session receives the outcome of every reset; only the session that sent it is told.
    public Task Consume(ClientContext<DeploymentStateReset> context, CancellationToken cancellationToken)
    {
        if (context.Message.CorrelationId == _resetCorrelationId)
            OnDeploymentStateReset?.Invoke(this, context.Message);

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

    public async Task ResetSagaState(Guid clusterId, string requestedBy, CancellationToken token)
    {
        var command = new ResetSagaState(clusterId, requestedBy);
        _resetCorrelationId = command.CorrelationId;

        await _mediator.Send(command, token);
    }

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
