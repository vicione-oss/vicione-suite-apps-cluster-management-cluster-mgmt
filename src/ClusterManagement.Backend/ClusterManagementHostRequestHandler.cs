using ClusterManagement.Backend.Engines;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend;

internal sealed class ClusterManagementHostRequestHandler(EngineLifetime engineLifetime) : IModuleHostRequestHandler
{
    public Task OnReset(CancellationToken stoppingToken = default)
        => engineLifetime.Stop(stoppingToken);

    public Task OnRestore(CancellationToken stoppingToken = default)
        => engineLifetime.Stop(stoppingToken);
}
