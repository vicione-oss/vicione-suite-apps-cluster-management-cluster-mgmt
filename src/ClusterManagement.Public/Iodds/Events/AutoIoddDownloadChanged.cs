using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Events;

[ForwardToUI]
public sealed record AutoIoddDownloadChanged(bool Value) : IEvent;
