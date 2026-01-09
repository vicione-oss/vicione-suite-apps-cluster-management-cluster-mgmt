using System.Runtime.Loader;

namespace ClusterManagement.Backend.Designs;

internal class AssemblyLoadContextObserver
{
    private const int ContextAliveInformationDelay = 20;
    private const int ContextAliveWarningDelay = 5000;

    internal static void Observe(AssemblyLoadContext context, Action logEarly, Action logLate, Action logUnloaded, int logEarlyDelay = ContextAliveInformationDelay, int logLateDelay = ContextAliveWarningDelay)
    {
        WeakReference contextReference = new(context, trackResurrection: true);
        Task.Run(async () =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(logEarlyDelay);

            if (contextReference.IsAlive)
            {
                logEarly();
            }
            else
            {
                logUnloaded();
                return;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(logLateDelay);

            if (contextReference.IsAlive)
                logLate();
            else
                logUnloaded();
        });
    }
}
