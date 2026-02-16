using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record AddOrUpdateClusterEditorSettings(EditorSettings Settings) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
