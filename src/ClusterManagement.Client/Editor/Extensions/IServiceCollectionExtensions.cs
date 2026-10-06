using ClusterManagement.Client.Editor.ControlPanels.General;
using ClusterManagement.Client.Editor.ControlPanels.General.Services;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.FunctionBlockTree;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using ViciOne.Ui.Blazor.Components.Sidebar.Extensions;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddEditor(this IServiceCollection services, string moduleId)
    {
        services.AddScoped<IClusterService, ClusterService>();

        // editor support services
        services.AddScoped<LoadClusterService>();
        services.AddScoped<SaveClusterService>();
        services.AddScoped<LoadFunctionBlocksService>();
        services.AddTransient<FunctionBlockTreeAdapter>();

        // FunctionBlock contract loading
        // wraps ISuiteRulesetProvider for ClusterEditor
        services.AddScoped<RulesetProviderWrapper>();
        services.AddClusterEditor<IRulesetProvider>(s => s.GetRequiredService<RulesetProviderWrapper>());
        // required by the ClusterEditor sidebar since ViciOne.Ui.Blazor.Components 6.x
        services.AddSidebar();

        // control panels
        services.AddControlPanel<ClusterManagementClientModule, GeneralControlPanel, GeneralControlPanelState>()
            .WithAutoDiscovery<GeneralControlPanelDescriptor>()
            .WithResetHandler<GeneralControlPanelResetHandler>()
            .WithSaveHandler<GeneralControlPanelSaveHandler>();

        services.AddPackagesControlPanel(moduleId);
        services.AddPackageControlPanel();

        return services;
    }

    internal static IServiceCollection AddPackagesControlPanel(this IServiceCollection services, string moduleId)
    {
        services.AddGridItemSelectColumn()
            .AddGridItemSelection<ClusterPackageInfo>(typeof(InstalledPackagesControlPanelServiceKey))
            .AddGridItemSelection<ClusterPackageInfo>(typeof(AvailablePackagesControlPanelServiceKey));

        services.AddControlPanel<ClusterManagementClientModule, PackagesControlPanel, PackagesControlPanelState>()
           .WithAutoDiscovery<PackagesControlPanelDescriptor>()
           .WithResetHandler<PackagesControlPanelResetHandler>()
            .WithSaveHandler<PackagesControlPanelSaveHandler>();

        services.AddStreamUploadHandler<ClusterPackageUpload>(moduleId, o =>
        {
            o.PathTransform = (p) => Path.Combine(p, ClusterPackageUpload.DirectoryName);
            // A unique name, so two uploads of the same file don't overwrite or delete each other.
            o.FilenameTransform = _ => $"{Guid.NewGuid():N}.zip";
        });

        return services;
    }

    internal static IServiceCollection AddPackageControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<ClusterManagementClientModule, PackageDetailsControlPanel, PackageDetailsControlPanelState>()
            .WithAutoDiscovery<PackageDetailsControlPanelDescriptor>()
            .WithResetHandler<PackageDetailsControlPanelResetHandler>()
            .WithSaveHandler<PackageDetailsControlPanelSaveHandler>();

        return services;
    }
}
