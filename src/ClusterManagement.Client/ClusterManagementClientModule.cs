using ClusterManagement.Client.DataflowEvents.Extensions;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.Localization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Public;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;
using Sdk.Journal;

namespace ClusterManagement.Client;

public sealed class ClusterManagementClientModule : ClientModule
{
    public override Action<IServiceCollection> Configure => services =>
    {
        services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>();
        services.AddNavTiles<ClusterManagementClientModule>();        
        services.AddScoped<IPackageService, PackageService>();

        services
            .AddClusterManagement()
            .AddDataflowEvents()
            .AddEditor()
            .AddIoddManagement();
    };

    public override Func<IServiceProvider, Task>? InitializeServices => services =>
    {
        var options = services.GetRequiredService<IOptions<ClusterManagementOptions>>().Value;
        var registry = services.GetRequiredService<INavTileRegistry<ClusterManagementClientModule>>();
        if (options.HideClusterEditorNavTile)
            registry.Remove(Constants.EditorNavTileId);

        services.GetRequiredService<IJournalMonitoring>()
            .AddFilterEntry(Constants.EngineHostJournalFilterKey, ClusterTerms.EngineHost, Constants.EngineHostJournalFilter, 10);

        return Task.CompletedTask;
    };
}
