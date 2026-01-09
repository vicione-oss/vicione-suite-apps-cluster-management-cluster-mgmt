using MassTransit;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public partial class DeployClusterStateMachine
{
    private void LogEvent<TMessage>(BehaviorContext<DeployClusterState, TMessage> context, string message) where TMessage : class
        => LogEventInfo(_logger, context.CorrelationId, context.Message.GetType().Name, message);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Saga[{CorrelationId}][Start] deploying cluster={ClusterId} target version={TargetVersion}, delete previous version={DeletePrevious}, use options={HasOptions}")]
    internal static partial void LogInit(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, Version? targetVersion, bool deletePrevious, bool hasOptions);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Saga[{CorrelationId}] Init cluster={ClusterId} deployment failed")]
    internal static partial void LogInitFailed(ILogger<DeployClusterStateMachine> logger, Exception ex, Guid? correlationId, Guid clusterId);


    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] transition from {CurrentState} to {NextState}")]
    internal static partial void LogTransition(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? currentState, string nextState);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}][{EventName}] {Message}")]
    internal static partial void LogEventInfo(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string eventName, string? message);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Saga[{CorrelationId}][End] deploying cluster={ClusterId} took {Duration}")]
    internal static partial void LogDeployDuration(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, TimeSpan duration);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] deployment of '{TargetVersion}' in progress. Version '{RejectedVersion}' rejected")]
    internal static partial void LogDeployRejected(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version targetVersion, Version? rejectedVersion);


    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Saga[{CorrelationId}] deployment of target version {Version} failed")]
    internal static partial void LogClusterUpdatedFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version version);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] update application {ApplicationId} successful")]
    internal static partial void LogApplicationUpdated(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid applicationId);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Saga[{CorrelationId}] failed to update application {ApplicationId} with '{Message}'")]
    internal static partial void LogApplicationUpdateFailure(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid applicationId, string? message);


    [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] Send commit version '{CommitVersion}'")]
    internal static partial void LogSendCommitVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version commitVersion);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] Send remove committed version '{CommittedVersion}'")]
    internal static partial void LogSendRemoveCommittedVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] Removed committed version '{CommittedVersion}'")]
    internal static partial void LogRemovedCommitCluster(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(EventId = 23, Level = LogLevel.Error, Message = "Saga[{CorrelationId}] Failed to remove committed version '{CommittedVersion}'")]
    internal static partial void LogRemoveCommitClusterFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version committedVersion);

    [LoggerMessage(EventId = 24, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}] Cluster {ClusterId} version '{CommittedVersion}' was created and will be used as deployment target version")]
    internal static partial void LogCreatedCommitVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid clusterId, Version committedVersion);

    [LoggerMessage(EventId = 25, Level = LogLevel.Error, Message = "Saga[{CorrelationId}] Failed to process committed cluster {ClusterId} version '{CommittedVersion}'")]
    internal static partial void LogCreatedCommitVersionFailed(ILogger<DeployClusterStateMachine> logger, Exception ex, Guid correlationId, Guid clusterId, Version committedVersion);


    [LoggerMessage(EventId = 30, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] Send remove previous version '{PreviousVersion}'")]
    internal static partial void LogSendRemovePreviousVersion(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);

    [LoggerMessage(EventId = 31, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] Removed previous active version '{PreviousVersion}'")]
    internal static partial void LogRemovedPreviousCluster(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);

    [LoggerMessage(EventId = 32, Level = LogLevel.Error, Message = "Saga[{CorrelationId}] Failed to remove previous active version '{PreviousVersion}'")]
    internal static partial void LogRemovePreviousClusterFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Version previousVersion);


    [LoggerMessage(EventId = 60, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] application {ApplicationId} updated")]
    internal static partial void LogProcessNodeDeploymentUpdated(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid? applicationId);

    [LoggerMessage(EventId = 61, Level = LogLevel.Warning, Message = "Saga[{CorrelationId}] application {ApplicationId} update failed")]
    internal static partial void LogProcessNodeDeploymentFailed(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, Guid? applicationId);


    [LoggerMessage(EventId = 65, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] all nodes={NodeIds} added")]
    internal static partial void LogAllNodesAdded(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? nodeIds);

    [LoggerMessage(EventId = 66, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}] waiting for nodes={NodeIds} to be added")]
    internal static partial void LogNodesAdded(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? nodeIds);


    [LoggerMessage(EventId = 67, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] all applications={ApplicationIds} started")]
    internal static partial void LogAllAppsStarted(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? applicationIds);

    [LoggerMessage(EventId = 68, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}] waiting for applications={ApplicationIds} to be started")]
    internal static partial void LogAppsStarted(ILogger<DeployClusterStateMachine> logger, Guid? correlationId, string? applicationIds);


    [LoggerMessage(EventId = 70, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] all applications={ApplicationIds} updated (errors={Error})")]
    internal static partial void LogAllDeploymentsUpdated(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string applicationIds, bool error);

    [LoggerMessage(EventId = 71, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}] waiting for applications={ApplicationIds} (errors={Error})")]
    internal static partial void LogDeploymentsUpdated(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string applicationIds, bool error);

    [LoggerMessage(EventId = 72, Level = LogLevel.Warning, Message = "Saga[{CorrelationId}] deployment failed on application={ApplicationId} with error '{Message}'")]
    internal static partial void LogDeploymentsError(ILogger<DeployClusterStateMachine> logger, Guid correlationId, Guid applicationId, string? message);


    [LoggerMessage(EventId = 75, Level = LogLevel.Information, Message = "Saga[{CorrelationId}] all nodes={NodeIds} cleaned up")]
    internal static partial void LogAllNodesCleaned(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string nodeIds);

    [LoggerMessage(EventId = 76, Level = LogLevel.Debug, Message = "Saga[{CorrelationId}] waiting for nodes={NodeIds} to be cleaned up")]
    internal static partial void LogNodesCleaned(ILogger<DeployClusterStateMachine> logger, Guid correlationId, string nodeIds);
}
