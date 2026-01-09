using System.Diagnostics;

namespace ClusterManagement.Backend.Diagnostics;

internal interface IProcessManager
{
    IProcess? GetProcess(int processId);
    IProcess StartProcess(ProcessStartInfo startInfo);
}
