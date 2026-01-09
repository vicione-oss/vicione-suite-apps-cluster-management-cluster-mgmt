using ClusterManagement.Public;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record RetrieveFunctionBlockParametersResponse(IReadOnlyCollection<FunctionBlockParameterValues> FunctionBlockParameters) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
