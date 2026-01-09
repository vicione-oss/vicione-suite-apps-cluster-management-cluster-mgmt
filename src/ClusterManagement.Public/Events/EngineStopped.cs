using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record EngineStopped(Guid EngineId) : IEvent;
