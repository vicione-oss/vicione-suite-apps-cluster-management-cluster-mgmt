using System.Diagnostics;

namespace ClusterManagement.Backend.Diagnostics;

internal sealed class RealProcessResult(Process process) : IProcessResult
{
    public int ExitCode { get; } = process.ExitCode;
    public Task<string> GetStandardOutput(CancellationToken cancellationToken) => process.StandardOutput.ReadToEndAsync(cancellationToken);
    public Task<string> GetStandardError(CancellationToken cancellationToken) => process.StandardError.ReadToEndAsync(cancellationToken);
}
