using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetClusterEditorSettingsRequest(string UserId) : IRequest<GetClusterEditorSettingsResponse>;
