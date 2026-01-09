using ClusterManagement.Backend.DeployCluster.Contracts;
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

    /// <summary>
    /// Rollback to last active cluster version sending <see cref="UpdateDeployments" /> commands to each application. 
    /// Finalize saga and publish <see cref="ClusterUpdateFailed"/>
    /// </summary>
    public State? RollingBackDeployments { get; }

    public Event<Public.Commands.DeployCluster>? UpdateRequested { get; }

    public Event<ClusterChanged>? CommitClusterChanged { get; }
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
        Event(() => UpdateRequested);
        Event(() => CommitClusterChanged);
        Event(() => CommitClusterFailed);
        Event(() => ClusterNodeAdded);
        Event(() => ClusterApplicationStarted);
        Event(() => NodeDeploymentsUpdated);
        Event(() => NodeDeploymentsUpdateFailed);
        Event(() => ClusterNodesCleaned);

        // This results in the following values: 0 - None, 1 - Initial, 2 - Final,
        // 3 - CommittingClusterChange,  4 - AddingClusterNodes, 5 - CleaningClusterNodes, 6 - UpdatingDeployments...
        InstanceState(x => x.CurrentState,
            CommittingCluster,
            AddingClusterNodes,
            UpdatingDeployments,
            CleaningClusterNodes,
            RollingBackDeployments);

        // We need to create cluster from options? -> CommittingCluster
        // We need to create additional nodes? -> AddingClusterNodes
        // No cluster node creation necessary? -> UpdateDeployments        

        Initially(
            When(UpdateRequested)
                .ThenAsync(StartDeploymentSaga)
                .IfElse(ctx => ctx.Saga.InitializeFailed,
                    binder => binder
                        // Initalize active or target cluster failed
                        .Then(BeforeFailedFinalize)
                        .Finalize(),
                    binder => binder
                    .IfElse(ctx => ctx.Message.Options != null,
                        innerBinder => innerBinder
                            // Target cluster from options need to be stored
                            .ThenAsync(SendCommitClusterCommand)
                            .ThenAsync(ctx => PublishTransition(ctx, nameof(CommittingCluster)))
                            .TransitionTo(CommittingCluster),
                        innerBinder => innerBinder
                            // Target cluster is already stored and available
                            .IfElse(ctx => ctx.Saga.ClusterNodesToCreate.Count > 0,
                                innerMostBinder => innerMostBinder
                                    .ThenAsync(SendAddClusterNodesCommand)
                                    .ThenAsync(ctx => PublishTransition(ctx, nameof(AddingClusterNodes)))
                                    .TransitionTo(AddingClusterNodes),
                                // at least one deployment failed
                                innerMostBinder => innerMostBinder
                                    // trigger update deployments to active version
                                    .PublishAsync(CreateUpdateDeploymentsTargetVersion)
                                    .ThenAsync(ctx => PublishTransition(ctx, nameof(UpdatingDeployments)))
                                    .TransitionTo(UpdatingDeployments)))
                    )
                );

        During(CommittingCluster,
            When(CommitClusterChanged)
                .ThenAsync(ClusterChangeCommitted)
                .IfElse(ctx => ctx.Saga.InitializeFailed,
                    binder => binder
                        // Target cluster was created but loading failed
                        .Then(BeforeFailedFinalize)
                        .Finalize(),
                    binder => binder
                        .IfElse(ctx => ctx.Saga.ClusterNodesToCreate.Count > 0,
                            innerBinder => innerBinder
                                .ThenAsync(SendAddClusterNodesCommand)
                                .ThenAsync(ctx => PublishTransition(ctx, nameof(AddingClusterNodes)))
                                .TransitionTo(AddingClusterNodes),
                            // at least one deployment failed
                            innerBinder => innerBinder
                                // trigger update deployments to active version
                                .PublishAsync(CreateUpdateDeploymentsTargetVersion)
                                .ThenAsync(ctx => PublishTransition(ctx, nameof(UpdatingDeployments)))
                                .TransitionTo(UpdatingDeployments))),
            When(CommitClusterFailed)
                .ThenAsync(PublishClusterUpdateFailed)
                .Then(BeforeFailedFinalize)
                .Finalize()
            );

        // Saga will stay in this state till all nodes are created and apps are running
        During(AddingClusterNodes,
            When(ClusterNodeAdded)
                .ThenAsync(AddingClusterNodesWhenClusterNodeAdded)
                .If(ctx => AreAllNodesAddedAndAppsRunning(ctx.Saga),
                    binder => binder
                        .PublishAsync(CreateUpdateDeploymentsTargetVersion)
                        .ThenAsync(ctx => PublishTransition(ctx, nameof(UpdatingDeployments)))
                        .TransitionTo(UpdatingDeployments)),
            When(ClusterApplicationStarted)
                .ThenAsync(AddingClusterNodesWhenClusterApplicationStarted)
                .If(ctx => AreAllNodesAddedAndAppsRunning(ctx.Saga),
                    binder => binder
                        .PublishAsync(CreateUpdateDeploymentsTargetVersion)
                        .ThenAsync(ctx => PublishTransition(ctx, nameof(UpdatingDeployments)))
                        .TransitionTo(UpdatingDeployments)),
            When(UpdateRequested)
                .ThenAsync(PublishDeployClusterRejected)
        );

        // Update deployments on all nodes/apps
        // -> finalize when all are updated
        // -> rollback on fail
        During(UpdatingDeployments,
            When(NodeDeploymentsUpdated)
                .ThenAsync(ProcessNodeDeploymentUpdated)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga),
                    binder => binder
                        // all deployments seem to be updated
                        .IfElse(ctx => !ctx.Saga.AtLeastOneDeployReturnedError,
                            innerBinder => innerBinder
                                .IfElse(ctx => ctx.Saga.ClusterNodesToClean.Count > 0,
                                        innermostBinder => innermostBinder
                                            .ThenAsync(ctx => SendCleanClusterNodesCommand(ctx, ctx.Saga.ClusterNodesToClean))
                                            .ThenAsync(ctx => PublishTransition(ctx, nameof(CleaningClusterNodes)))
                                            .TransitionTo(CleaningClusterNodes),
                                        innermostBinder => innermostBinder
                                            .ThenAsync(CompleteClusterUpdate)
                                            .Finalize()
                                    ),
                            // at least one deployment failed
                            innerBinder => innerBinder
                                // trigger update deployments to active version
                                .Then(ctx => ctx.Saga.ClusterApplicationsUpdateFinished.Clear())
                                .PublishAsync(CreateUpdateDeploymentsActiveVersion)
                                .ThenAsync(ctx => PublishTransition(ctx, nameof(RollingBackDeployments)))
                                .TransitionTo(RollingBackDeployments))),
            When(NodeDeploymentsUpdateFailed)
                .ThenAsync(ProcessNodeDeploymentFailed)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga),
                    binder => binder
                        // all deployments seem to be updated
                        .IfElse(ctx => !ctx.Saga.AtLeastOneDeployReturnedError,
                            innerBinder => innerBinder
                                .IfElse(ctx => ctx.Saga.ClusterNodesToClean.Count > 0,
                                    innermostBinder => innermostBinder
                                        .ThenAsync(ctx => SendCleanClusterNodesCommand(ctx, [.. ctx.Saga.ClusterNodesCreated]))
                                        .ThenAsync(ctx => PublishTransition(ctx, nameof(CleaningClusterNodes)))
                                        .TransitionTo(CleaningClusterNodes),
                                    innermostBinder => innermostBinder
                                        .ThenAsync(CompleteClusterUpdate)
                                        .Finalize()
                                ),
                            // at least one deployment failed
                            innerBinder => innerBinder
                                // trigger update deployments to active version
                                .Then(ctx => ctx.Saga.ClusterApplicationsUpdateFinished.Clear())
                                .PublishAsync(CreateUpdateDeploymentsActiveVersion)
                                .ThenAsync(ctx => PublishTransition(ctx, nameof(RollingBackDeployments)))
                                .TransitionTo(RollingBackDeployments))),
            When(UpdateRequested)
                .ThenAsync(PublishDeployClusterRejected),
            Ignore(ClusterNodeAdded),
            Ignore(ClusterApplicationStarted)
        );

        // Only processed if UpdatingDeployments was succesfull and we have nodes to be cleaned up
        During(CleaningClusterNodes,
            When(ClusterNodesCleaned)
                .Then(ProcessClusterNodeCleaned)
                .If(ctx => AreAllClusterNodesCleaned(ctx.Saga),
                    binder => binder
                        .ThenAsync(CompleteClusterUpdate)
                        .Finalize()),
           When(UpdateRequested)
                .ThenAsync(PublishDeployClusterRejected));

        // Triggered by UpdatingDeployments if at least one deployment failed
        During(RollingBackDeployments,
            When(NodeDeploymentsUpdated)
                .Then(ProcessRollbackNodeDeploymentUpdated)//TODO: Rollback cluster node deployments
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga),
                    binder => binder
                        .ThenAsync(PublishClusterUpdateFailed)
                        .Then(BeforeFailedFinalize)
                        .Finalize()),
            // this case should never happen but we have to handle it!
            When(NodeDeploymentsUpdateFailed)
                .Then(ProcessRollbackNodeDeploymentFailed)
                .If(ctx => AreAllDeploymentsUpdated(ctx.Saga),
                    binder => binder
                        .ThenAsync(PublishClusterUpdateFailed)
                        .Then(BeforeFailedFinalize)
                        .Finalize()),
            When(UpdateRequested)
                .ThenAsync(PublishDeployClusterRejected));

        SetCompletedWhenFinalized();
    }

    private static Dictionary<ClusterNode, ClusterApplication?> GetClusterNodeAndCoreOsApplicationIds(Cluster cluster)
        => cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .ToDictionary(n => n,
                n => n.Applications.FirstOrDefault(a => a.IsCoreOs()));

    public static string GetState(int sagaState) => sagaState switch
    {
        0 => "None",
        1 => nameof(Initial),
        2 => nameof(Final),
        3 => nameof(CommittingCluster),
        4 => nameof(AddingClusterNodes),
        5 => nameof(UpdatingDeployments),
        6 => nameof(CleaningClusterNodes),
        7 => nameof(RollingBackDeployments),
        _ => "Unknown",
    };
}
