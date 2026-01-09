using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record EngineTornDown(Guid EngineId) : IEvent;
