using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record AutoIoddDownloadChanged(bool Value) : IEvent;
