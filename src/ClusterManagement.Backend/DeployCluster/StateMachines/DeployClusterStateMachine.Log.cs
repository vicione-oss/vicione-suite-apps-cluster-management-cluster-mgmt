using MassTransit;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public partial class DeployClusterStateMachine
{
    private void LogEvent<TMessage>(BehaviorContext<DeployClusterState, TMessage> context, string message) where TMessage : class
        => LogEventInfo(_logger, context.CorrelationId, context.Message.GetType().Name, message);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}][Start] deploying cluster={ClusterId} target version={TargetVersion}, delete previous version={DeletePrevious}, use options={HasOptions}")]
    internal static partial void LogInit(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, Version? targetVersion, bool deletePrevious, bool hasOptions);

    [LoggerMessage(LogLevel.Error, "Saga[{CorrelationId}] deployment of cluster={ClusterId} failed in state {State}")]
    internal static partial void LogDeploymentStepFailed(ILogger<DeployClusterStateMachine> logger, Exception ex, Guid correlationId, Guid clusterId, string state);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] transition from {CurrentState} to {NextState}")]
    internal static partial void LogTransition(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? currentState, string nextState);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] ignored duplicate deployment reply of application {ApplicationId}")]
    internal static partial void LogDuplicateDeploymentReply(ILogger<DeployClusterStateMachine> logger, Guid correlationId, Guid applicationId);

    [LoggerMessage(LogLevel.Warning, "Saga[{CorrelationId}] ignored unexpected event {EventName} in state {State}")]
    internal static partial void LogUnhandledEvent(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string eventName, string state);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}][{EventName}] {Message}")]
    internal static partial void LogEventInfo(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string eventName, string? message);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}][End] deploying cluster={ClusterId} took {Duration}")]
    internal static partial void LogDeployDuration(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, TimeSpan duration);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] deployment of '{TargetVersion}' in progress. Version '{RejectedVersion}' rejected")]
    internal static partial void LogDeployRejected(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version targetVersion, Version? rejectedVersion);


    [LoggerMessage(LogLevel.Warning, "Saga[{CorrelationId}] deployment of target version {Version} failed")]
    internal static partial void LogClusterUpdatedFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version version);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] update application {ApplicationId} successful")]
    internal static partial void LogApplicationUpdated(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid applicationId);

    [LoggerMessage(LogLevel.Error, "Saga[{CorrelationId}] failed to update application {ApplicationId} with '{Message}'")]
    internal static partial void LogApplicationUpdateFailure(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid applicationId, string? message);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] Send commit version '{CommitVersion}'")]
    internal static partial void LogSendCommitVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version commitVersion);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] Send remove committed version '{CommittedVersion}'")]
    internal static partial void LogSendRemoveCommittedVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] Removed committed version '{CommittedVersion}'")]
    internal static partial void LogRemovedCommitCluster(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(LogLevel.Error, "Saga[{CorrelationId}] Failed to remove committed version '{CommittedVersion}'")]
    internal static partial void LogRemoveCommitClusterFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] Cluster {ClusterId} version '{CommittedVersion}' was created and will be used as deployment target version")]
    internal static partial void LogCreatedCommitVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, Version committedVersion);



    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] Send remove previous version '{PreviousVersion}'")]
    internal static partial void LogSendRemovePreviousVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);

    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] Removed previous active version '{PreviousVersion}'")]
    internal static partial void LogRemovedPreviousCluster(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);

    [LoggerMessage(LogLevel.Error, "Saga[{CorrelationId}] Failed to remove previous active version '{PreviousVersion}'")]
    internal static partial void LogRemovePreviousClusterFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] application {ApplicationId} updated")]
    internal static partial void LogProcessNodeDeploymentUpdated(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid? applicationId);

    [LoggerMessage(LogLevel.Warning, "Saga[{CorrelationId}] application {ApplicationId} update failed")]
    internal static partial void LogProcessNodeDeploymentFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid? applicationId);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] all nodes={NodeIds} added")]
    internal static partial void LogAllNodesAdded(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? nodeIds);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] waiting for nodes={NodeIds} to be added")]
    internal static partial void LogNodesAdded(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? nodeIds);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] all applications={ApplicationIds} started")]
    internal static partial void LogAllAppsStarted(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? applicationIds);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] waiting for applications={ApplicationIds} to be started")]
    internal static partial void LogAppsStarted(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? applicationIds);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] all applications={ApplicationIds} updated (errors={Error})")]
    internal static partial void LogAllDeploymentsUpdated(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string applicationIds, bool error);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] waiting for applications={ApplicationIds} (errors={Error})")]
    internal static partial void LogDeploymentsUpdated(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string applicationIds, bool error);

    [LoggerMessage(LogLevel.Warning, "Saga[{CorrelationId}] deployment failed on application={ApplicationId} with error '{Message}'")]
    internal static partial void LogDeploymentsError(ILogger<DeployClusterStateMachine> logger, Guid correlationId, Guid applicationId, string? message);


    [LoggerMessage(LogLevel.Information, "Saga[{CorrelationId}] all nodes={NodeIds} cleaned up")]
    internal static partial void LogAllNodesCleaned(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string nodeIds);

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] waiting for nodes={NodeIds} to be cleaned up")]
    internal static partial void LogNodesCleaned(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string nodeIds);
}
