using System.ComponentModel.DataAnnotations;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;


public sealed class DeployClusterState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    // If using Optimistic concurrency, this property is required
    [Timestamp]
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[]? RowVersion { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
    public int CurrentState { get; set; }

    public Guid ClusterId { get; set; }

    public Version ActiveVersion { get; set; } = new("0.0.1");
    public Version TargetVersion { get; set; } = new("0.0.1");
    public DateTime Started { get; set; }

    public List<Guid> ClusterApplicationsToUpdate { get; } = [];
    public Dictionary<Guid, ErrorInfo?> ClusterApplicationsUpdateFinished { get; } = [];

    public Dictionary<Guid, Guid?> ClusterNodesToCreate { get; } = [];
    public HashSet<Guid> ClusterNodesCreated { get; } = [];
    public HashSet<Guid> ClusterApplicationStarted { get; } = [];

    public List<Guid> ClusterNodesToClean { get; } = [];
    public HashSet<Guid> ClusterNodesCleaned { get; } = [];

    public bool AtLeastOneDeployReturnedError { get; set; }

    public bool InitializeFailed { get; set; }
}
