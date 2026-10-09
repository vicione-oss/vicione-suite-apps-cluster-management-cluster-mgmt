using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using MassTransit;
using MassTransit.Testing;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Extensions;

internal static class ITestHarnessExtensions
{
    public static async Task PublishClusterChangedCreated(this ITestHarness harness, Guid correlationId, Guid clusterId, Version targetVersion)
        => await harness.Bus.Publish(new ClusterChanged(correlationId, clusterId, CrudAction.Created, targetVersion));

    public static async Task PublishClusterChangeFailed(this ITestHarness harness, Guid correlationId, Guid clusterId, Version targetVersion)
        => await harness.Bus.Publish(new CommitClusterChangeFailed(correlationId, clusterId, targetVersion, new ErrorInfo(42, "TestFailure")));

    public static async Task PublishClusterNodeAdded(this ITestHarness harness, Guid correlationId, Guid clusterId, Guid nodeId)
        => await harness.Bus.Publish(new ClusterNodeAddedEvent(correlationId, clusterId, nodeId));

    public static async Task PublishClusterApplicationStarted(this ITestHarness harness, Guid correlationId, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterApplicationStartedEvent(clusterId, applicationId) { CorrelationId = correlationId });

    public static async Task PublishClusterNodeDeploymentsUpdated(this ITestHarness harness, Guid correlationId, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterNodeDeploymentsUpdatedEvent(correlationId, clusterId, applicationId));

    public static async Task PublishClusterNodeDeploymentsUpdateFailed(this ITestHarness harness, Guid correlationId, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterNodeDeploymentsUpdateFailedEvent(correlationId, clusterId, applicationId, new ErrorInfo(42, "TestFailure")));

    public static async Task PublishClusterNodeCleaned(this ITestHarness harness, Guid correlationId, Guid clusterId, Guid nodeId)
        => await harness.Bus.Publish(new ClusterNodeCleanedEvent(correlationId, clusterId, nodeId));

    public static async Task PublishClusterDeploymentCompleted(this ITestHarness harness, Guid correlationId, Guid clusterId, Version newVersion)
        => await harness.Bus.Publish(new ClusterUpdateCompleted(correlationId, clusterId, newVersion, TimeSpan.FromSeconds(1)));

    public static async Task DeployCluster(this ITestHarness harness, Guid correlationId, Guid clusterId, Version? targetVersion, DeployClusterOptions? options = null, bool deletePreviousVersion = false)
        => await harness.Bus.Publish(new Public.Commands.DeployCluster(clusterId, targetVersion)
        {
            CorrelationId = correlationId,
            Options = options,
            DeletePreviousVersionOnSuccess = deletePreviousVersion
        });

    public static async Task PublishClusterChangedDeleted(this ITestHarness harness, Guid correlationId, Guid clusterId, Version removedVersion)
        => await harness.Bus.Publish(new ClusterChanged(correlationId, clusterId, CrudAction.Deleted, removedVersion));

    public static async Task PublishClusterErrorDeleteFailed(this ITestHarness harness, Guid correlationId, Guid clusterId, Version? version = null)
        => await harness.Bus.Publish(new ClusterError { ClusterId = clusterId, Error = new ErrorInfo(ClusterError.DeleteFailed, "Failure"), CorrelationId = correlationId, Version = version });

    public static async Task AssertDeployClusterRejected(this ITestHarness harness, Guid correlationId, Version rejectedVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateRejected>(k => k.Context.CorrelationId == correlationId
        && k.Context.Message.RejectedVersion == rejectedVersion, TestContext.Current.CancellationToken),
            $"Reject deploy cluster version {rejectedVersion} event not published");

    public static async Task AssertClusterUpdateCompleted(this ITestHarness harness, Guid correlationId, Version? targetVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateCompleted>(k => k.Context.CorrelationId == correlationId
        && (targetVersion is null || k.Context.Message.NewVersion == targetVersion), TestContext.Current.CancellationToken),
            $"Deploy cluster version '{targetVersion}' completed event not published");

    public static async Task AssertClusterUpdateFailed(this ITestHarness harness, Guid correlationId, Version? targetVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == correlationId
        && (targetVersion is null || k.Context.Message.Version == targetVersion), TestContext.Current.CancellationToken),
            $"Deploy cluster version '{targetVersion}' failed event not published");

    public static Task AssertDeployClusterState(this ITestHarness harness, Guid correlationId, Func<DeployClusterStateMachine, State?> stateSelector)
    {
        var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
        return sagaHarness.AssertState(correlationId, stateSelector);
    }

    public static async Task AssertState(this ISagaStateMachineTestHarness<DeployClusterStateMachine, DeployClusterState> sagaHarness, Guid correlationId, Func<DeployClusterStateMachine, State?> stateSelector)
    {
        var expectedState = stateSelector.Invoke(sagaHarness.StateMachine)
            ?? throw new InvalidOperationException("Invalid saga state");

        var sagaId = await sagaHarness.Exists(correlationId, expectedState);
        Assert.True(sagaId.HasValue, $"Saga did not exist in state {expectedState.Name}");
    }

    public static async Task<DeployClusterState> GetDeployClusterState(this ITestHarness harness, CancellationToken cancellationToken = default)
    {
        var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
        var sagas = sagaHarness.Sagas.SelectAsync(cancellationToken);
        var instance = sagas.First();

        return (await instance).Saga;
    }
}
