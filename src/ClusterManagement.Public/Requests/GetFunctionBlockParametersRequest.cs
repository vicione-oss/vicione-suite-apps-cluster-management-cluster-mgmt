using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetFunctionBlockParametersRequest(Guid ClusterId, IReadOnlyCollection<Guid> FunctionBlockIds) : IRequest<GetFunctionBlockParametersResponse>;
