using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetEngineLogRequest(Guid ClusterId, Guid EngineId)
    : IInstanceDependentRequest<GetEngineLogResponse>;
