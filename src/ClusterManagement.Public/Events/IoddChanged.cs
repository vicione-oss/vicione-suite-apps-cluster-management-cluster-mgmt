using ClusterManagement.Public.Iodds;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record IoddChanged(IoddId Iodd, DeviceInfo? DeviceInfo, CrudAction Action) : IEvent;
