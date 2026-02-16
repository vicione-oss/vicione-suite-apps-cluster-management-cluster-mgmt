
using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record StopEngineOnInstance(Guid EngineId) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
