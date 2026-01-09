namespace ClusterManagement.Backend.Diagnostics;

internal interface IProcess : IDisposable
{
    int Id { get; }
    bool IsRunning { get; }
    Task<IProcessResult> WaitForExit(CancellationToken cancellationToken);
    void Kill();
}
