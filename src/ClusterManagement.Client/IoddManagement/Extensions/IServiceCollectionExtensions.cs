using ClusterManagement.Client.IoddManagement.ControlPanels;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.Extensions;

namespace ClusterManagement.Client.IoddManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddIoddManagement(this IServiceCollection services, string moduleId)
    {
        services.AddControlPanel<ClusterManagementClientModule, IoddControlPanel, IoddControlPanelState>()
            .WithAutoDiscovery<IoddControlPanelDescriptor>()
            .WithResetHandler<IoddControlPanelResetHandler>()
            .WithSaveHandler<IoddControlPanelSaveHandler>();

        services.AddControlPanel<ClusterManagementClientModule, IoddDetailsControlPanel, IoddDetailsControlPanelState>()
            .WithAutoDiscovery<IoddDetailsControlPanelDescriptor>();

        services.AddScoped<IIoddService, IoddService>();
        services.AddTransient<IoddTreeAdapter>();
        services.AddStreamUploadHandler<IoddUpload>(moduleId, (o) =>
        {
            o.PathTransform = (p) => Path.Combine(p, IoddService.UploadedIoddDirectoryName);
            o.FilenameTransform = null;
        });

        return services;
    }
}
