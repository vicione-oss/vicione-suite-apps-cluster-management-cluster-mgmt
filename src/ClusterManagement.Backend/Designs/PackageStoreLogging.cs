using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Designs;

internal static partial class PackageStoreLogging
{
    [LoggerMessage(LogLevel.Information, "Package {Name} supports Framework: {TargetFramework}, Runtime Version: {RuntimeVersion}, Platform Target: {PlatformTarget}")]
    internal static partial void LogPackageVerification(this ILogger<PackageStore> logger, string name, string? targetFramework, string? runtimeVersion, Machine platformTarget);

    [LoggerMessage(LogLevel.Warning, "Failed to verify upload package assembly for '{Name}'")]
    internal static partial void LogPackageVerificationFailure(this ILogger<PackageStore> logger, string name);

    [LoggerMessage(LogLevel.Warning, "Removing existing package '{Name}' path '{Path}'")]
    internal static partial void LogPackageRemovingWarning(this ILogger<PackageStore> logger, string name, string? path);

    [LoggerMessage(LogLevel.Information, "Uploading package '{Name}' version '{Version}' to path '{Path}'")]
    internal static partial void LogPackageUpload(this ILogger<PackageStore> logger, string name, string version, string? path);

    [LoggerMessage(LogLevel.Warning, "Failed to delete missing package '{Name}' with path '{Path}'")]
    internal static partial void LogDeletePackageFailureWarning(this ILogger<PackageStore> logger, string name, string path);

    [LoggerMessage(LogLevel.Information, "Deleting package '{Name}' version '{Version}' with path '{Path}'")]
    internal static partial void LogPackageVersionDeletion(this ILogger<PackageStore> logger, string name, string version, string? path);

    [LoggerMessage(LogLevel.Information, "Deleting empty package folder '{Name}' with path '{Path}'")]
    internal static partial void LogEmptyPackageFolderDeletion(this ILogger<PackageStore> logger, string name, string? path);
}
