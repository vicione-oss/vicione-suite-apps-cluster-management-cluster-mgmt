using System.Diagnostics;

namespace ClusterManagement.Backend.Diagnostics;

internal sealed class RealProcess(Process process) : IProcess
{
    public int Id => process.Id;
    public bool IsRunning
    {
        get
        {
            var isRunning = false;
            try
            {
                _ = process.Id;
                isRunning = !process.HasExited;
            }
            catch
            {
                // There is no process associated with the process object.
            }
            return isRunning;
        }
    }

    public async Task<IProcessResult> WaitForExit(CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken);
        return new RealProcessResult(process);
    }

    public void Kill() => process.Kill(true);
    public void Dispose() => process.Dispose();
}
