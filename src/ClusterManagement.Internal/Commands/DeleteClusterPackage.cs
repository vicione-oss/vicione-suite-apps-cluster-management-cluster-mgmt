using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Commands;

public sealed record DeleteClusterPackage(string PackageName, string Version) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
