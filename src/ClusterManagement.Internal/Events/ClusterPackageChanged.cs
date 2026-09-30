using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterPackageChanged(string PackageName, string Version, ClusterPackageChange Change, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public const int UnknownError = 0;
    public const int AddPackageFailed = 200;
    public const int PackageFileNotFound = 201;
    public const int NotAZipArchive = 202;
    public const int EmptyArchive = 203;
    public const int CorruptArchive = 204;
    public const int InvalidPackageContent = 205;
    public const int PackageVersionInUse = 206;
    public const int PackageFileOutsideUploadDirectory = 207;
}

public enum ClusterPackageChange
{
    Add,
    Delete,
    Update,
}
