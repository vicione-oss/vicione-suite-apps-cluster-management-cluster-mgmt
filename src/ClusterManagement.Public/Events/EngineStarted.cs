using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record EngineStarted(Guid EngineId) : IEvent;
