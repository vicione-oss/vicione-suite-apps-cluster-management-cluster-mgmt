using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Events;

[ForwardToUI]
public sealed record IoddChanged(IoddId Iodd, DeviceInfo? DeviceInfo, CrudAction Action) : IEvent;
