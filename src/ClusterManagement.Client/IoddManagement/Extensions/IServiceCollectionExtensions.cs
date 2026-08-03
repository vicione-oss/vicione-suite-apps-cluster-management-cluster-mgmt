using ClusterManagement.Client.IoddManagement.ControlPanels;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace ClusterManagement.Client.IoddManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddIoddManagement(this IServiceCollection services)
    {
        services.AddControlPanel<ClusterManagementClientModule, IoddControlPanel, IoddControlPanelState>()
            .WithAutoDiscovery<IoddControlPanelDescriptor>()
            .WithResetHandler<IoddControlPanelResetHandler>()
            .WithSaveHandler<IoddControlPanelSaveHandler>();

        services.AddControlPanel<ClusterManagementClientModule, IoddDetailsControlPanel, IoddDetailsControlPanelState>()
            .WithAutoDiscovery<IoddDetailsControlPanelDescriptor>();

        services.AddScoped<IIoddService, IoddService>();
        services.AddTransient<IoddTreeAdapter>();

        return services;
    }
}
