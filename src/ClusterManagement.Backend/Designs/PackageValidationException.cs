using System.Diagnostics.CodeAnalysis;

namespace ClusterManagement.Backend.Designs;

/// <summary>
/// Thrown when an uploaded package archive is invalid. Retrying cannot succeed, so this is treated as a permanent failure.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors")] //Analyser does not recognize optional parameters
public sealed class PackageValidationException(int errorCode, string message, Exception? innerException = null) :
    Exception(message, innerException)
{
    /// <summary>Gets one of the error codes defined on <see cref="Internal.Events.ClusterPackageChanged"/>.</summary>
    public int ErrorCode { get; } = errorCode;
}
