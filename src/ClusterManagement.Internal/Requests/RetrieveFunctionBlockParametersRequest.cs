using ClusterManagement.Public;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record RetrieveFunctionBlockParametersRequest(IReadOnlyCollection<FunctionBlockParameterIds> FunctionBlockParameters)
    : IInstanceDependentRequest<RetrieveFunctionBlockParametersResponse>;
