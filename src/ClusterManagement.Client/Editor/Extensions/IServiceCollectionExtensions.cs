using ClusterManagement.Client.Editor.ControlPanels;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
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

        return services;
    }
}
