using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

[SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed record CommitClusterChange(byte[] CompressedCluster, CommitClusterMetaInfo MetaInfo, CommitClusterOptions? Options) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
