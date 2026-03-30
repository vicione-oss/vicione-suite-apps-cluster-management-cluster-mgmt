using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record GetPackagesOnInstance(Guid EngineId) : IInstanceDependentCommand
{
    Guid IInstanceDependentCommand.CorrelationId { get; init; } = Guid.NewGuid();
}
