using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

public sealed record ToggleEngine(Guid EngineId, bool Start) : ICommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
