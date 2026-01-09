
using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record StartEngineOnInstance(Guid EngineId) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
