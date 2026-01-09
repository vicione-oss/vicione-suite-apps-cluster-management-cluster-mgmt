using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

[SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed record UpdateDeployments(byte[] CompressedTargetCluster, Guid CorrelationId, Guid ClusterId) : IInstanceDependentCommand;
