using ClusterManagement.Client.IoddManagement.ControlPanels;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace ClusterManagement.Client.IoddManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddIoddManagement(this IServiceCollection services)
    {
        services.AddControlPanel<ClusterManagementClientModule, IoddControlPanel, IoddControlPanelState>()
            .WithAutoDiscovery<IoddControlPanelDescriptor>();
        services.AddControlPanel<ClusterManagementClientModule, IoddDetailsControlPanel, IoddDetailsControlPanelState>()
            .WithAutoDiscovery<IoddDetailsControlPanelDescriptor>();

        return services;
    }
}
