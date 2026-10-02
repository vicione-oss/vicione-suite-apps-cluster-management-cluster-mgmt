using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Client.DataflowEvents.Extensions;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.Localization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal;
using ClusterManagement.Public;
using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using ClusterManagement.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Client.Connections;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;
using Sdk.Connections.Contracts;
using Sdk.Journal;
using ViciOne.Ui.Blazor.Components.Draggable.Extensions;

namespace ClusterManagement.Client;

public sealed class ClusterManagementClientModule : ClientModule
{
    public override Action<IServiceCollection> Configure => services =>
    {
        services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>();
        services.AddNavTiles<ClusterManagementClientModule>();
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<IResourceDownloadStateService, ResourceDownloadStateService>();

        services.AddDraggable();

        services
            .AddClusterManagement()
            .AddDataflowEvents()
            .AddEditor(ModuleId)
            .AddIoddManagement(ModuleId);
    };

    public override Func<IServiceProvider, Task>? InitializeServices => services =>
    {
        var options = services.GetRequiredService<IOptions<ClusterManagementOptions>>().Value;
        var registry = services.GetRequiredService<INavTileRegistry<ClusterManagementClientModule>>();
        if (options.HideClusterEditorNavTile)
            registry.Remove(Constants.EditorNavTileId);

        var connectionTypeUiRegistry = services.GetRequiredService<IConnectionTypeUiRegistry>();
        connectionTypeUiRegistry.Register<OpcUaClientConnection, OpcUaClientConnectionSettings, OpcUaClientConnectionValidator>(
            ConnectionType.OpcUaClient, () => OpcUaConnectionSettings.DisplayNameClient);
        connectionTypeUiRegistry.Register<OpcUaServerConnection, OpcUaServerConnectionSettings, OpcUaServerConnectionValidator>(
            ConnectionType.OpcUaServer, () => OpcUaConnectionSettings.DisplayNameServer);

        services.GetRequiredService<IJournalMonitoring>()
            .AddFilterEntry(Constants.EngineHostJournalFilterKey, ClusterTerms.EngineHost, Constants.EngineHostJournalFilter, 10);

        return Task.CompletedTask;
    };
}
