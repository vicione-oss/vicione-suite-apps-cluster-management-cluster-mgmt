using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

/// <summary>
/// Represents a command to deploy a cluster to a target version, optionally retaining the previous version after success.
/// </summary>
/// <param name="ClusterId">The ID of the cluster to be deployed.</param>
/// <param name="TargetVersion">The target version of the cluster to deploy to. May be <see langword="null"/> to use the latest or default version.</param>
[MessageEndpoint(Constants.DeployClusterState)]
public sealed record DeployCluster(Guid ClusterId, Version? TargetVersion) : ICommand
{
    /// <summary>
    /// Gets the correlation ID for this command, which is the same as the cluster ID.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets or initializes additional deployment options.
    /// </summary>
    public DeployClusterOptions? Options { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the previously deployed version should be retained
    /// if the deployment completes successfully.
    /// </summary>
    public bool DeletePreviousVersionOnSuccess { get; set; }
}
