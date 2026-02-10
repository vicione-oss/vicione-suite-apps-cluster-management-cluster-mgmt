using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Commands;

public sealed record ResetSagaState(Guid ClusterId) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
