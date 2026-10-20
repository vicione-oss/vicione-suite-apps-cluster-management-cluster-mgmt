using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

/// <summary>
/// Sent by the deploy saga timeout watchdog to a deploy cluster saga that stayed in its state longer than the state's timeout.
/// </summary>
public sealed record DeployClusterStateTimeout(Guid CorrelationId) : CorrelatedBy<Guid>;
