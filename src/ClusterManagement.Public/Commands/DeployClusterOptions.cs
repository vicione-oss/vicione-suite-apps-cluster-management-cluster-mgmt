using System.Diagnostics.CodeAnalysis;

namespace ClusterManagement.Public.Commands;

[SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed record DeployClusterOptions(byte[] CompressedCluster);
