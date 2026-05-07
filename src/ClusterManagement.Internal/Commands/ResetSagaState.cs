using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record ResetSagaState(Guid ClusterId) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
