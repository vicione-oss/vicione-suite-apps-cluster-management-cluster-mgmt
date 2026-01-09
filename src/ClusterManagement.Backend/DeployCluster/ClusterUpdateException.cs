using System.Diagnostics.CodeAnalysis;

namespace ClusterManagement.Backend.DeployCluster;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors")] //Analyser does not recognize optional parameters
public sealed class ClusterUpdateException(string? message = null, Exception? innerException = null, int errorCode = -1) :
    Exception(message, innerException)
{
    public int ErrorCode { get; } = errorCode;
}
