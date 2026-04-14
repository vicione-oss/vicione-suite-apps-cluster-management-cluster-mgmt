using ClusterManagement.Client.Editor.ControlPanels.General;
using ClusterManagement.Client.Editor.ControlPanels.General.Services;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddEditor(this IServiceCollection services)
    {
        services.AddScoped<IClusterService, ClusterService>();

        // editor support services
        services.AddScoped<LoadClusterService>();
        services.AddScoped<ExportClusterService>();
        services.AddScoped<SaveClusterService>();
        services.AddScoped<LoadFunctionBlocksService>();

        // wizards - will be handled inside editor one day
        services.AddScoped<ClusterWizardDialogService>();
        services.AddScoped<ImportWizardService>();

        // FunctionBlock contract loading
        // wraps ISuiteRulesetProvider for ClusterEditor
        services.AddScoped<RulesetProviderWrapper>();
        services.AddClusterEditor<IRulesetProvider>(s => s.GetRequiredService<RulesetProviderWrapper>());

        // control panels
        services.AddControlPanel<ClusterManagementClientModule, GeneralControlPanel, GeneralControlPanelState>()
            .WithAutoDiscovery<GeneralControlPanelDescriptor>()
            .WithResetHandler<GeneralControlPanelResetHandler>()
            .WithSaveHandler<GeneralControlPanelSaveHandler>();

        services.AddPackagesControlPanel();
        services.AddPackageControlPanel();

        return services;
    }

    internal static IServiceCollection AddPackagesControlPanel(this IServiceCollection services)
    {
        services.AddGridItemSelectColumn()
            .AddGridItemSelection<ClusterPackageInfo>(typeof(InstalledPackagesControlPanelServiceKey))
            .AddGridItemSelection<ClusterPackageInfo>(typeof(AvailablePackagesControlPanelServiceKey));

        services.AddControlPanel<ClusterManagementClientModule, PackagesControlPanel, PackagesControlPanelState>()
           .WithAutoDiscovery<PackagesControlPanelDescriptor>()
           .WithResetHandler<PackagesControlPanelResetHandler>()
            .WithSaveHandler<PackagesControlPanelSaveHandler>();

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
