using System.Diagnostics;

namespace ClusterManagement.Backend.Diagnostics;

internal sealed class ProcessManager : IProcessManager
{
    public IProcess? GetProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return new RealProcess(process);
        }
        catch (ArgumentException)
        {
            // The process is not running or the id might be expired.
            return null;
        }
    }

    public IProcess StartProcess(ProcessStartInfo startInfo)
    {
#pragma warning disable CA2000 // Objekte verwerfen, bevor Bereich verloren geht. Lebenszeit wird durch den Aufrufer verwaltet.
        Process process = new()
        {
            StartInfo = startInfo,
        };
#pragma warning restore CA2000 // Objekte verwerfen, bevor Bereich verloren geht
        process.Start();
        return new RealProcess(process);
    }
}
