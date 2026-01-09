namespace ClusterManagement.Backend.Diagnostics;

internal interface IProcessResult
{
    int ExitCode { get; }
    Task<string> GetStandardOutput(CancellationToken cancellationToken);
    Task<string> GetStandardError(CancellationToken cancellationToken);
}
