using ClusterManagement.Public.ClusterEditor;
using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterEditorSettingsResponse : IResponse
{
    public Settings? Settings { get; init; }
    public ErrorInfo? RequestError { get; init; }
}
