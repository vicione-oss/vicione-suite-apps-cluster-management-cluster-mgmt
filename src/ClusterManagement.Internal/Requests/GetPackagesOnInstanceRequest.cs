using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetPackagesOnInstanceRequest(Guid EngineId) : IInstanceDependentRequest<GetPackagesOnInstanceResponse>;
