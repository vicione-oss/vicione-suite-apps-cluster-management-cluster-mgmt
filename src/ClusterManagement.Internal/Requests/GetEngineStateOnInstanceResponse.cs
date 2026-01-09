using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetEngineStateOnInstanceResponse : IResponse
{
    public GetEngineStateOnInstanceResponse()
    { }

    public GetEngineStateOnInstanceResponse(ErrorInfo errorInfo)
        => RequestError = errorInfo;

    public EngineState? EngineState { get; init; }
    public ErrorInfo? RequestError { get; init; }
}

public enum EngineState
{
    Stopped,
    Running,
    Starting,
    Stopping,
}
