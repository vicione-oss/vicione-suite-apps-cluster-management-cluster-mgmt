using System.Diagnostics.CodeAnalysis;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Thrown when a request on an OPC UA certificate store cannot succeed, e.g. because of an invalid store path or certificate.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors")] //Analyser does not recognize optional parameters
public sealed class OpcUaCertificateException(int errorCode, string message, Exception? innerException = null) :
    Exception(message, innerException)
{
    /// <summary>Gets one of the error codes defined on <see cref="Internal.Contracts.OpcUaCertificateError"/>.</summary>
    public int ErrorCode { get; } = errorCode;
}
