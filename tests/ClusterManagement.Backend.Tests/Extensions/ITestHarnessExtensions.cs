using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using MassTransit;
using MassTransit.Testing;
using Sdk.Messaging;
using Xunit;

namespace ClusterManagement.Backend.Tests.Extensions;

internal static class ITestHarnessExtensions
{
    public static async Task PublishClusterChanged(this ITestHarness harness, Guid clusterId, Version targetVersion)
        => await harness.Bus.Publish(new ClusterChanged(clusterId, CrudAction.Created, targetVersion) { CorrelationId = clusterId });

    public static async Task PublishClusterChangeFailed(this ITestHarness harness, Guid clusterId, Version targetVersion)
        => await harness.Bus.Publish(new CommitClusterChangeFailed(clusterId, targetVersion, new ErrorInfo(42, "TestFailure")));

    public static async Task PublishClusterNodeAdded(this ITestHarness harness, Guid clusterId, Guid nodeId)
        => await harness.Bus.Publish(new ClusterNodeAddedEvent(clusterId, nodeId));

    public static async Task PublishClusterApplicationStarted(this ITestHarness harness, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterApplicationStartedEvent(clusterId, applicationId));

    public static async Task PublishClusterNodeDeploymentsUpdated(this ITestHarness harness, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterNodeDeploymentsUpdatedEvent(clusterId, applicationId));

    public static async Task PublishClusterNodeDeploymentsUpdateFailed(this ITestHarness harness, Guid clusterId, Guid applicationId)
        => await harness.Bus.Publish(new ClusterNodeDeploymentsUpdateFailedEvent(clusterId, applicationId, new ErrorInfo(42, "TestFailure")));

    public static async Task PublishClusterNodeCleaned(this ITestHarness harness, Guid clusterId, Guid nodeId)
        => await harness.Bus.Publish(new ClusterNodeCleanedEvent(clusterId, nodeId));

    public static async Task PublishClusterDeploymentCompleted(this ITestHarness harness, Guid clusterId, Version newVersion)
        => await harness.Bus.Publish(new ClusterUpdateCompleted(clusterId, newVersion, TimeSpan.FromSeconds(1)));

    public static async Task DeployCluster(this ITestHarness harness, Guid clusterId, Version targetVersion, DeployClusterOptions? options = null)
        => await harness.Bus.Publish(new Public.Commands.DeployCluster(clusterId, targetVersion) { Options = options });

    public static async Task AssertDeployClusterRejected(this ITestHarness harness, Guid clusterId, Version rejectedVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateRejected>(k => k.Context.CorrelationId == clusterId && k.Context.Message.RejectedVersion == rejectedVersion),
            $"Reject deploy cluster version {rejectedVersion} event not published");

    public static async Task AssertClusterUpdateCompleted(this ITestHarness harness, Guid clusterId, Version? targetVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateCompleted>(k => k.Context.CorrelationId == clusterId
        && (targetVersion is null || k.Context.Message.NewVersion == targetVersion)),
            $"Deploy cluster version '{targetVersion}' completed event not published");

    public static async Task AssertClusterUpdateFailed(this ITestHarness harness, Guid clusterId, Version? targetVersion)
        => Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == clusterId
        && (targetVersion is null || k.Context.Message.Version == targetVersion)),
            $"Deploy cluster version '{targetVersion}' failed event not published");

    public static Task AssertDeployClusterState(this ITestHarness harness, Guid clusterId, Func<DeployClusterStateMachine, State?> stateSelector)
    {
        var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
        return sagaHarness.AssertState(clusterId, stateSelector);
    }

    public static async Task AssertState(this ISagaStateMachineTestHarness<DeployClusterStateMachine, DeployClusterState> sagaHarness, Guid clusterId, Func<DeployClusterStateMachine, State?> stateSelector)
    {
        var expectedState = stateSelector.Invoke(sagaHarness.StateMachine)
            ?? throw new InvalidOperationException("Invalid saga state");

        var sagaId = await sagaHarness.Exists(clusterId, expectedState);
        Assert.True(sagaId.HasValue, $"Saga did not exist in state {expectedState.Name}");
    }
}
