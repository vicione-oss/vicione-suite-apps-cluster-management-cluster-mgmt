using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetEngineStateOnInstanceRequest(Guid EngineId) : IInstanceDependentRequest<GetEngineStateOnInstanceResponse>;
