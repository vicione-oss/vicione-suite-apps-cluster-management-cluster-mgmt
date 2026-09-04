using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

/// <summary>
/// Represents an event that encapsulates error information related to cluster operations such as publishing, updating, or querying.
/// </summary>
public sealed record ClusterError : IEvent, CorrelatedBy<Guid?>
{
    // Error code constants

    /// <summary>Represents an unspecified or unknown error.</summary>
    public const int UnknownError = 0;

    /// <summary>No cluster ID provided for the requested version.</summary>
    public const int MissingClusterIdForVersion = 100;

    /// <summary>The provided cluster ID is unknown.</summary>
    public const int ClusterIdUnknown = 101;

    /// <summary>No cluster exists for the requested version.</summary>
    public const int NoClusterForVersion = 102;

    /// <summary>Unexpected error occurred while querying a single cluster.</summary>
    public const int UnexpectedClusterQueryError = 104;

    /// <summary>Unexpected error occurred while querying multiple clusters.</summary>
    public const int UnexpectedClustersQueryError = 105;

    /// <summary>Either the cluster ID or dataflow ID must be provided.</summary>
    public const int ClusterOrDataflowIdRequired = 106;

    /// <summary>Communication is not properly configured for the cluster or engine.</summary>
    public const int CommunicationUnconfigured = 121;

    /// <summary>The communication method is unknown.</summary>
    public const int CommunicationUnknown = 122;

    /// <summary>The communication type could not be converted as required.</summary>
    public const int CommunicationUnconvertible = 123;

    /// <summary>Publishing the cluster or dataflow failed.</summary>
    public const int PublishFailed = 200;

    /// <summary>Publishing failed because the same version already exists.</summary>
    public const int PublishFailedVersionVersionExists = 202;

    /// <summary>Updating deployment failed.</summary>
    public const int UpdateDeploymentFailed = 210;

    /// <summary>Deployment update failed because the target engine was not found.</summary>
    public const int UpdateDeploymentFailedEngineNotFound = 211;

    /// <summary>Updating the auto-start setting failed.</summary>
    public const int UpdateAutoStartFailed = 220;

    /// <summary>Auto-start update failed because the deployment is not present.</summary>
    public const int UpdateAutoStartFailedNotDeployed = 221;

    /// <summary>Cluster deletion failed.</summary>
    public const int DeleteFailed = 300;

    /// <summary>Function block designs failed to load.</summary>
    public const int DesignsFailedToLoad = 400;

    /// <summary>A dependency package failed to load.</summary>
    public const int PackageFailedToLoad = 401;

    /// <summary>Editor failed to load cluster settings.</summary>
    public const int EditorLoadSettingsFailed = 500;

    /// <summary>Editor failed to store cluster settings.</summary>
    public const int EditorStoreSettingsFailed = 501;

    /// <summary>
    /// Gets or initializes the correlation ID used to trace related operations.
    /// </summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// Gets or initializes the ID of the cluster where the error occurred.
    /// </summary>
    public Guid? ClusterId { get; init; }

    /// <summary>
    /// Gets or initializes the version of the cluster involved in the operation.
    /// </summary>
    public Version? Version { get; init; }

    /// <summary>
    /// Gets or initializes additional error metadata including messages and codes.
    /// </summary>
    public ErrorInfo? Error { get; init; }
}

