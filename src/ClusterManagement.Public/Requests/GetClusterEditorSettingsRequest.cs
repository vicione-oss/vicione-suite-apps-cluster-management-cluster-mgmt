using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterEditorSettingsRequest(string UserId) : IRequest<GetClusterEditorSettingsResponse>;
