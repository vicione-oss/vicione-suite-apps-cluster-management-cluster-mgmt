using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

public sealed record ClusterError : IEvent, CorrelatedBy<Guid?>
{
    public const int UnknownError = 0;
    public const int MissingClusterIdForVersion = 100;
    public const int ClusterIdUnknown = 101;
    public const int NoClusterForVersion = 102;
    public const int ClusterNotActive = 103;
    public const int UnexpectedClusterQueryError = 104;
    public const int UnexpectedClustersQueryError = 105;
    public const int ClusterOrDataflowIdRequired = 106;
    public const int EngineUnknown = 110;
    public const int CommunicationUnconfigured = 121;
    public const int CommunicationUnknown = 122;
    public const int CommunicationUnconvertible = 123;
    public const int LogUnattainable = 130;
    public const int PublishFailed = 200;
    public const int PublishFailedVersionVersionExists = 202;
    public const int UpdateDeploymentFailed = 210;
    public const int UpdateDeploymentFailedEngineNotFound = 211;
    public const int UpdateAutoStartFailed = 220;
    public const int UpdateAutoStartFailedNotDeployed = 221;
    public const int DeleteFailed = 300;
    public const int DesignsFailedToLoad = 400;
    public const int EditorLoadSettingsFailed = 500;
    public const int EditorStoreSettingsFailed = 501;

    public Guid? CorrelationId { get; init; }
    public Guid? ClusterId { get; init; }
    public ErrorInfo? Error { get; init; }
}
