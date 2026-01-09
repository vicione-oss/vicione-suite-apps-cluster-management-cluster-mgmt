using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetClusterEditorSettingsResponse : IResponse
{
    public EditorSettings? Settings { get; init; }
    public ErrorInfo? RequestError { get; init; }
}
