using ClusterManagement.Public.ClusterEditor;
using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

public sealed record AddOrUpdateClusterEditorSettings(Settings Settings) : ICommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
