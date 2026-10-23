using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

[SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed record UpdateDeployments(byte[] CompressedTargetCluster, Guid CorrelationId, Guid ClusterId, Version ClusterVersion) : IInstanceDependentCommand
{
    /// <summary>
    /// The update restores the version that ran before; a command sent before this flag existed counts as the update.
    /// </summary>
    public bool IsRollback { get; init; }
}
