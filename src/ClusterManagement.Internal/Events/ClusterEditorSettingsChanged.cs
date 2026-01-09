using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterEditorSettingsChanged(EditorSettings? Settings, CrudAction CrudAction) : IEvent;
