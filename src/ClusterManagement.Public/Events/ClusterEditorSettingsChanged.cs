using ClusterManagement.Public.ClusterEditor;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterEditorSettingsChanged(Settings? Settings, CrudAction CrudAction) : IEvent;
