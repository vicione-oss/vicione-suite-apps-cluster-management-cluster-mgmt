using ClusterManagement.Internal.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterEditorSettingsChanged(EditorSettings? Settings, CrudAction CrudAction, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
};
