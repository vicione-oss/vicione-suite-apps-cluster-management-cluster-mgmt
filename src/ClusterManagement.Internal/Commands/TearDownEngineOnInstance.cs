using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record TearDownEngineOnInstance(Guid EngineId) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
