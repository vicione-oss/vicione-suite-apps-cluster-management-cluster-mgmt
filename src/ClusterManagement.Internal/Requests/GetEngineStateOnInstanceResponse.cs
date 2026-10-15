using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetEngineStateOnInstanceResponse : IResponse
{
    public GetEngineStateOnInstanceResponse()
    { }

    public GetEngineStateOnInstanceResponse(ErrorInfo errorInfo)
        => RequestError = errorInfo;

    public EngineState? EngineState { get; init; }

    /// <summary>
    /// Id the engine host knows the engine by, for example as chain link in its cycle reports; set whenever the engine
    /// is deployed on the instance, also with <see cref="Public.ErrorHandling.ClusterErrors.EngineHostDidNotAnswer"/>.
    /// </summary>
    public Guid? DeploymentId { get; init; }
    public ErrorInfo? RequestError { get; init; }
}

public enum EngineState
{
    Stopped,
    Running,
    Starting,
    Stopping,

    /// <summary>
    /// The engine host could not start the engine; appended, so the values before keep their numbers.
    /// </summary>
    StartFailed,
}
