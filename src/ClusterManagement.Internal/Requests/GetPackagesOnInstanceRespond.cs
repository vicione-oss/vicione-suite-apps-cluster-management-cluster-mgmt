using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetPackagesOnInstanceResponse : IResponse
{
    public GetPackagesOnInstanceResponse()
    { }

    public GetPackagesOnInstanceResponse(ErrorInfo errorInfo)
    => RequestError = errorInfo;

    public ErrorInfo? RequestError { get; init; }

    public IReadOnlyDictionary<string, string>? Packages { get; init; }
}
