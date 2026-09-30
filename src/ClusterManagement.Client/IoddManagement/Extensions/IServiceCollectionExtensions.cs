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
            o.PathTransform = (p) => Path.Combine(p, IoddUpload.DirectoryName);
            // A unique name, so two uploads of the same file don't overwrite or delete each other.
            o.FilenameTransform = _ => $"{Guid.NewGuid():N}.zip";
        });

        return services;
    }
}
