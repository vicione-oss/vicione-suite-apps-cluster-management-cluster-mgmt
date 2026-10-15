using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Extensions;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Extensions;
using MassTransit;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

// ReSharper disable UnassignedGetOnlyAutoProperty
// ReSharper disable MemberCanBePrivate.Global
public sealed partial class DeployClusterStateMachine : MassTransitStateMachine<DeployClusterState>
{
    /// <summary>
    /// Persisted <see cref="DeployClusterState.CurrentState"/> values are the index in this list.
    /// Only append new states: reordering or removing breaks sagas that are already stored.
    /// </summary>
    private static readonly string[] s_persistedStateNames =
    [
        "None",
        nameof(Initial),
        nameof(Final),
        nameof(CommittingCluster),
        nameof(AddingClusterNodes),
        nameof(UpdatingDeployments),
        nameof(CleaningClusterNodes),
        nameof(CleaningCluster),
        nameof(RollingBackDeployments),
        nameof(RollingBackCommit),
        nameof(RollingBackClusterNodes),
    ];

    // None, Initial and Final are defined by MassTransit
    private const int FirstCustomStateValue = 3;

    private readonly ILogger<DeployClusterStateMachine> _logger;
    private readonly IServiceProvider _services;

    /// <summary>
    /// State is only executed if <see cref="Public.Commands.DeployCluster"/> has options containing a compressed cluster json.
    /// this state will send <see cref="Public.Commands.CommitClusterChange"/> command to create the target cluster version.
    /// Transistion to <see cref="AddingClusterNodes"/>
    /// Transistion to <see cref="UpdatingDeployments"/>
    /// </summary>
    public State? CommittingCluster { get; }

    /// <summary>
    /// If target cluster has nodes to be added this step will send <see cref="AddClusterNodesCommand"/> to these nodes (instances).
    /// Process <see cref="ClusterNodeAddedEvent"/> and <see cref="ClusterApplicationStartedEvent"/> until all nodes and applications had been started.
    /// Transistion to <see cref="UpdatingDeployments"></see>
    /// </summary>
    public State? AddingClusterNodes { get; }

    /// <summary>
    /// This step sends <see cref="UpdateDeployments" /> commands with target version to each application. 
    /// Process <see cref="ClusterNodeDeploymentsUpdatedEvent"/> and <see cref="ClusterNodeDeploymentsUpdateFailedEvent"/> until all deployments had finished.    
    /// Finalize saga when all deployments were updated successfully and publish <see cref="ClusterUpdateCompleted"/>.
    /// Transition to <see cref="CleaningClusterNodes"/> when no deployment failed and nodes need to be cleaned up.     
    /// Transition to <see cref="RollingBackDeployments"/> when at least one deployment failed.
    /// </summary>
    public State? UpdatingDeployments { get; }

    /// <summary>
    /// Remove nodes we don't need anymore. Finalize the saga when all nodes are cleaned.
    /// </summary>
    public State? CleaningClusterNodes { get; }

    public State? CleaningCluster { get; }

    /// <summary>
    /// Rollback to last active cluster version sending <see cref="UpdateDeployments" /> commands to each application. 
    /// Finalize saga and publish <see cref="ClusterUpdateFailed"/>
    /// </summary>
    public State? RollingBackDeployments { get; }

    public State? RollingBackCommit { get; }

    /// <summary>
    /// After the deployments were rolled back, sends <see cref="CleanClusterNodesCommand"/> for the nodes this saga created
    /// and waits for a <see cref="ClusterNodeCleanedEvent"/> per node.
    /// Continue with <see cref="RollingBackCommit"/> or finalize and publish <see cref="ClusterUpdateFailed"/>.
    /// </summary>
    public State? RollingBackClusterNodes { get; }

    public Event<Public.Commands.DeployCluster>? UpdateRequested { get; }

    public Event<ClusterChanged>? ClusterChanged { get; }

    public Event<ClusterError>? ClusterError { get; }

    public Event<CommitClusterChangeFailed>? CommitClusterFailed { get; }
    public Event<ClusterNodeAddedEvent>? ClusterNodeAdded { get; }
    public Event<ClusterApplicationStartedEvent>? ClusterApplicationStarted { get; }
    public Event<ClusterNodeDeploymentsUpdatedEvent>? NodeDeploymentsUpdated { get; }
    public Event<ClusterNodeDeploymentsUpdateFailedEvent>? NodeDeploymentsUpdateFailed { get; }
    public Event<ClusterNodeCleanedEvent>? ClusterNodesCleaned { get; }


    public DeployClusterStateMachine(IServiceProvider services, ILogger<DeployClusterStateMachine> logger)
    {
        _services = services;
        _logger = logger;

        // Register the state events
        // Every deploy command carries its own correlation id; it joins a deployment of the same cluster that is still
        // running, which rejects it. Only one deployment per cluster can exist because the saga endpoint processes one
        // message at a time (ConcurrentMessageLimit = 1 in ClusterManagementBackendModule).
        Event(() => UpdateRequested, e => e
            .CorrelateBy((saga, ctx) => saga.ClusterId == ctx.Message.ClusterId)
            .SelectId(ctx => ctx.Message.CorrelationId));
        Event(() => ClusterChanged);
        Event(() => CommitClusterFailed);
        Event(() => ClusterError);
        Event(() => ClusterNodeAdded);
        Event(() => ClusterApplicationStarted);
        Event(() => NodeDeploymentsUpdated);
        Event(() => NodeDeploymentsUpdateFailed);
        Event(() => ClusterNodesCleaned);

        // The index in PersistedStateNames is the persisted CurrentState value
        InstanceState(x => x.CurrentState,
            [.. s_persistedStateNames[FirstCustomStateValue..].Select(name => GetState(name))]);

        // Target cluster from options? -> CommittingCluster
        // Nodes to create? -> AddingClusterNodes
        // Otherwise -> UpdatingDeployments
        Initially(
            When(UpdateRequested)
                .ThenAsync(StartDeploymentSaga)
                .IfElse(ctx => ctx.Message.Options != null,
                    commit => TransitionWithNotification(commit.ThenAsync(SendCommitClusterCommand), CommittingCluster!),
                    ContinueWithTargetCluster)
                // Any failure while starting fails the deployment instead of faulting the command
                .Catch<Exception>(ex => FailDeployment(ex, nameof(Initial))));

        During(CommittingCluster,
            ContinueWithTargetCluster(When(ClusterChanged)
                    .ThenAsync(ClusterChangeCommitted))
                // Any failure while loading the committed cluster fails the deployment
                .Catch<Exception>(ex => FailDeployment(ex, nameof(CommittingCluster))),
            When(CommitClusterFailed)
                .ThenAsync(PublishCommitClusterFailed)
                .Then(BeforeFailedFinalize)
                .Finalize());

        // Stay until all nodes are created and their apps are running
        During(AddingClusterNodes,
            When(ClusterNodeAdded)
                .ThenAsync(AddingClusterNodesWhenClusterNodeAdded)
                .If(ctx => AreAllNodesAddedAndAppsRunning(ctx.Saga), StartUpdatingDeployments)
                .Catch<Exception>(ex => FailDeployment(ex, nameof(AddingClusterNodes))),
            When(ClusterApplicationStarted)
                .ThenAsync(AddingClusterNodesWhenClusterApplicationStarted)
                .If(ctx => AreAllNodesAddedAndAppsRunning(ctx.Saga), StartUpdatingDeployments)
                .Catch<Exception>(ex => FailDeployment(ex, nameof(AddingClusterNodes))));

        // Update deployments on all nodes/apps, then clean up or roll back
        During(UpdatingDeployments,
            When(NodeDeploymentsUpdated)
                .ThenAsync(ProcessNodeDeploymentUpdated)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga), ContinueAfterDeploymentsUpdated)
                .Catch<Exception>(ex => FailDeployment(ex, nameof(UpdatingDeployments))),
            When(NodeDeploymentsUpdateFailed)
                .ThenAsync(ProcessNodeDeploymentFailed)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga), ContinueAfterDeploymentsUpdated)
                .Catch<Exception>(ex => FailDeployment(ex, nameof(UpdatingDeployments))),
            Ignore(ClusterNodeAdded),
            Ignore(ClusterApplicationStarted));

        // Only entered if all deployments succeeded and nodes have to be cleaned up
        During(CleaningClusterNodes,
            When(ClusterNodesCleaned)
                .Then(ProcessClusterNodeCleaned)
                .If(ctx => AreAllClusterNodesCleaned(ctx.Saga, ctx.Saga.ClusterNodesToClean), RemovePreviousVersionOrComplete));

        // Only entered if the previous version should be removed and is not the default version
        During(CleaningCluster,
            // Previous cluster version was deleted
            When(ClusterChanged)
                .If(ctx => IsPreviousVersionDeleted(ctx.Saga, ctx.Message), deleted => deleted
                    .Then(PreviousClusterDeleted)
                    .ThenAsync(CompleteClusterUpdate)
                    .Finalize()),
            // Previous cluster version delete failed: the deployment itself succeeded
            When(ClusterError)
                .If(ctx => IsPreviousVersionDeleteFailed(ctx.Saga, ctx.Message), deleteFailed => deleteFailed
                    .Then(PreviousClusterDeleteFailed)
                    .ThenAsync(CompleteClusterUpdate)
                    .Finalize()));

        // Entered from UpdatingDeployments if at least one deployment failed
        During(RollingBackDeployments,
            When(NodeDeploymentsUpdated)
                .Then(ProcessRollbackNodeDeploymentUpdated)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga), ContinueAfterDeploymentsRolledBack),
            // This case should never happen but we have to handle it!
            When(NodeDeploymentsUpdateFailed)
                .Then(ProcessRollbackNodeDeploymentFailed)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga), ContinueAfterDeploymentsRolledBack));

        // Entered from RollingBackDeployments if the saga created cluster nodes
        During(RollingBackClusterNodes,
            When(ClusterNodesCleaned)
                .Then(ProcessClusterNodeCleaned)
                .If(ctx => AreAllClusterNodesCleaned(ctx.Saga, ctx.Saga.ClusterNodesCreated), ContinueAfterClusterNodesRolledBack));

        // Entered from RollingBackDeployments if the saga committed a cluster version
        During(RollingBackCommit,
            // Committed cluster version was deleted
            When(ClusterChanged)
                .Then(CommitClusterDeleted)
                .If(ctx => ctx.Saga.IsCommitVersionEmpty(), PublishFailedAndFinalize),
            // Committed cluster version delete failed
            When(ClusterError)
                .Then(CommitClusterDeleteFailed)
                .If(ctx => ctx.Saga.IsCommitVersionEmpty(), failed => PublishFailedAndFinalize(failed, failedVersionKept: true)));

        // Only one deployment per saga: reject further requests while one is running
        DuringAny(
            When(UpdateRequested)
                .ThenAsync(PublishDeployClusterRejected));

        // Late or duplicate events must not fault the message
        OnUnhandledEvent(ctx =>
        {
            LogUnhandledEvent(_logger, ctx.Saga.CorrelationId, ctx.Event.Name, GetState(ctx.Saga.CurrentState));
            return ctx.Ignore();
        });

        SetCompletedWhenFinalized();
    }

    private static Dictionary<ClusterNode, ClusterApplication?> GetClusterNodeAndCoreOsApplicationIds(Cluster cluster)
        => cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .ToDictionary(n => n,
                n => n.Applications.FirstOrDefault(a => a.IsCoreOs()));

    /// <summary>
    /// Gets the name of a persisted <see cref="DeployClusterState.CurrentState"/> value.
    /// </summary>
    public static string GetState(int sagaState)
        => sagaState >= 0 && sagaState < s_persistedStateNames.Length ? s_persistedStateNames[sagaState] : "Unknown";
}
