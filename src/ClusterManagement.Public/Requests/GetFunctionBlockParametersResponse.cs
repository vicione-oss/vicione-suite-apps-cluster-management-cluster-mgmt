using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetFunctionBlockParametersResponse(IReadOnlyCollection<FunctionBlockParameterValues> FunctionBlockParameters) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
