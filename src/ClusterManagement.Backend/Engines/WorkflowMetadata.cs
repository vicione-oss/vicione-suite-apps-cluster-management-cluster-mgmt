namespace ClusterManagement.Backend.Engines;

internal sealed class WorkflowMetadata
{
    internal required List<Guid> MqttRetainTopics { get; init; }
}
