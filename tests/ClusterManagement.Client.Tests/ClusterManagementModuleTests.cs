using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Sdk.Client.Modules.Localization;
using Sdk.Testing.Client;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Tests;

public class ClusterManagementModuleTests
{
    [Fact]
    public async Task Init_module_should_register_and_configure_editor_services()
    {
        // Arrange
        var module = new ClusterManagementClientModule();

        // Act
        await using var serviceProvider = module.TestModuleInitialization(setup =>
        {
            setup.Services.AddClusterEditor(s => Substitute.For<IRulesetProvider>());
            setup.Services.AddSingleton(Substitute.For<ISuiteRulesetProvider>());
            setup.Services.AddSingleton(Substitute.For<IDesignProvider>()); //Registered via Backend
            setup.Services.AddSingleton(Substitute.For<IJSRuntime>());
        });

        // Assert - Editor
        Assert.NotNull(serviceProvider.GetRequiredService<IClusterService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRulesetProvider>());

        Assert.NotNull(serviceProvider.GetRequiredService<LoadClusterService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ExportClusterService>());
        Assert.NotNull(serviceProvider.GetRequiredService<SaveClusterService>());
        Assert.NotNull(serviceProvider.GetRequiredService<LoadFunctionBlocksService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ClusterWizardDialogService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ImportWizardService>());

        // Assert - Management
        Assert.NotNull(serviceProvider.GetRequiredService<IClusterManagementService>());

        // Assert - EventBroker
        Assert.NotNull(serviceProvider.GetRequiredService<DataflowEventPageService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventMqttClientProvider>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventMqttHandler>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventBroker>());
        Assert.NotNull(serviceProvider.GetRequiredService<IEventBroker>());

        // Assert - Iodd
        Assert.NotNull(serviceProvider.GetRequiredService<MonochromeIconHttpClient>());
        Assert.NotNull(serviceProvider.GetRequiredService<IIoddService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IoddTreeAdapter>());
    }

    [Fact]
    public void Should_register_localization()
    {
        // Arrange
        var module = new ClusterManagementClientModule();

        // Act
        using var serviceProvider = module.TestModuleInitialization(static setup => { });
        var localizer = serviceProvider.GetRequiredService<IClientModuleLocalizer<ClusterManagementClientModule>>();

        // Assert
        Assert.NotNull(localizer);
        localizer.GetTitle().Should().NotBeNullOrEmpty();
    }
}
