using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using ViciOne.Cluster.Builder;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

/// <summary>
/// A step and the engine host or engine it works on; an engine id of <see cref="Guid.Empty"/> stands for an engine the
/// instance does not know.
/// </summary>
internal readonly record struct DeploymentStepTarget(ClusterNodeDeploymentStep Step, Guid? EngineHostId = null, Guid? EngineId = null);

/// <summary>
/// Publishes the steps of one instance's deployment update for the UI. It remembers the step in progress, so the
/// consumer reports a failure once and the step it happened in is published with it.
/// </summary>
/// <remarks>
/// The events go through the bus, not the consume context: the consume context holds every publish back until the
/// consumer returns, which would deliver the whole progress at once at the end. The scoped <c>ISuiteMediator</c> is
/// held back the same way, because its publish endpoint inside a consumer is the consume context's. Publishing is best
/// effort and never fails the deployment: after the first publish that fails or does not confirm in time, the steps of
/// this update are not published at all, as a broker in that state would hold up every step for the full timeout.
/// </remarks>
internal sealed class DeploymentStepReporter(IBus bus, UpdateDeployments message, Guid clusterApplicationId, TimeProvider timeProvider,
    ILogger<UpdateDeploymentsConsumer> logger, CancellationToken cancellationToken)
{
    private static readonly TimeSpan s_publishTimeout = TimeSpan.FromSeconds(5);

    private Dictionary<Guid, string> _engineHostNames = [];
    private Dictionary<Guid, string> _engineNames = [];
    private DeploymentStepTarget? _current;
    private bool _brokerUnavailable;

    /// <summary>
    /// Names the engine hosts and engines of the target cluster in the following events.
    /// </summary>
    public void UseNamesOf(ClusterCache cache)
    {
        _engineHostNames = cache.EngineHosts.ToDictionary(h => h.Id, h => h.Name);
        _engineNames = cache.EngineIds.ToDictionary(e => e.Key, e => e.Value.Name);
    }

    public Task Begin(DeploymentStepTarget target)
    {
        _current = target;
        return Publish(target, ClusterNodeDeploymentStepStatus.Started);
    }

    public Task Complete()
    {
        if (_current is not { } current)
            return Task.CompletedTask;

        _current = null;
        return Publish(current, ClusterNodeDeploymentStepStatus.Succeeded);
    }

    /// <summary>
    /// Reports <paramref name="error"/> for the step in progress; a failure outside every step publishes nothing.
    /// </summary>
    public Task Fail(ErrorInfo error)
    {
        if (_current is not { } current)
            return Task.CompletedTask;

        _current = null;
        return Publish(current, ClusterNodeDeploymentStepStatus.Failed, error);
    }

    public async Task Run(DeploymentStepTarget target, Func<Task> step)
    {
        await Begin(target);
        await step();
        await Complete();
    }

    public async Task<T> Run<T>(DeploymentStepTarget target, Func<Task<T>> step)
    {
        await Begin(target);
        var result = await step();
        await Complete();
        return result;
    }

    /// <summary>
    /// Runs a step the engine host can refuse, which <paramref name="step"/> reports by returning <c>false</c>.
    /// </summary>
    public async Task<bool> RunUnlessRefused(DeploymentStepTarget target, Func<Task<bool>> step)
    {
        await Begin(target);
        var done = await step();
        if (done)
        {
            await Complete();
        }
        else
        {
            _current = null;
            await Publish(target, ClusterNodeDeploymentStepStatus.Refused);
        }

        return done;
    }

    private async Task Publish(DeploymentStepTarget target, ClusterNodeDeploymentStepStatus status, ErrorInfo? error = null)
    {
        if (_brokerUnavailable)
            return;

        var engineId = target.EngineId == Guid.Empty ? null : target.EngineId;
        ClusterNodeDeploymentStepChanged stepChanged = new(message.CorrelationId, message.ClusterId, clusterApplicationId, target.Step, status)
        {
            EngineHostId = target.EngineHostId,
            EngineHostName = NameOf(_engineHostNames, target.EngineHostId),
            EngineId = engineId,
            EngineName = NameOf(_engineNames, engineId),
            OccurredAt = timeProvider.GetUtcNow(),
            Error = error,
        };

        // A cancelled deployment still reports the step it stopped at, so only the other publishes end with it.
        var deploymentCancellation = status == ClusterNodeDeploymentStepStatus.Failed ? CancellationToken.None : cancellationToken;
        using CancellationTokenSource timeout = new(s_publishTimeout, timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, deploymentCancellation);
        try
        {
            await bus.Publish(stepChanged, cancellation.Token);
        }
        catch (OperationCanceledException) when (deploymentCancellation.IsCancellationRequested)
        {
            // The deployment was cancelled, which it reports itself.
        }
        catch (Exception e)
        {
            _brokerUnavailable = true;
            logger.LogStepNotPublished(e, target.Step, status, clusterApplicationId);
        }
    }

    private static string? NameOf(Dictionary<Guid, string> names, Guid? id)
        => id is { } key && names.TryGetValue(key, out var name) ? name : null;
}
