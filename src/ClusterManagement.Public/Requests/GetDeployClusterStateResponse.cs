using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed class GetDeployClusterStateResponse : IResponse
{
    /// <summary>
    /// Will be null when no deployment for requested cluster is running
    /// </summary>
    public DeploymentState? State { get; init; }

    public ErrorInfo? RequestError { get; init; }
}

