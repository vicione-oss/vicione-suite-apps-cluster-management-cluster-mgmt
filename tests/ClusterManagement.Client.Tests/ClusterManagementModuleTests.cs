using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Designs;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization;
using Sdk.Client.NavTiles.Services;
using Sdk.Testing.Client;
using ViciOne.Ui.ClusterEditor.Extensions;
using ViciOne.Ui.ClusterEditor.Services;
using Xunit;
using ClusterManagement.Client.Services;

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
            setup.Services.AddTransient(_ => new HttpClient());
            setup.Services.AddClusterEditor(s => Substitute.For<IRulesetProvider>());
            setup.Services.AddSingleton(Substitute.For<ISuiteRulesetProvider>());
            setup.Services.AddSingleton(Substitute.For<IDesignProvider>()); //Registered via Backend
            setup.Services.AddSingleton(Substitute.For<IJSRuntime>());
        }, HostingModel.BlazorServer);

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
        Assert.NotNull(serviceProvider.GetRequiredService<IClusterSagaHttpClient>());

        // Assert - EventBroker
        Assert.NotNull(serviceProvider.GetRequiredService<DataflowEventPageService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventMqttClientProvider>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventMqttHandler>());
        Assert.NotNull(serviceProvider.GetRequiredService<IDataflowEventBroker>());
        Assert.NotNull(serviceProvider.GetRequiredService<IEventBroker>());
    }

    [Fact]
    public void Should_register_nav_tiles()
    {
        // Arrange
        var module = new ClusterManagementClientModule();

        // Act
        using var serviceProvider = module.TestModuleInitialization();
        var navTileRegistry = serviceProvider.GetRequiredService<INavTileRegistry<ClusterManagementClientModule>>();

        // Assert
        Assert.NotNull(navTileRegistry);
        navTileRegistry.Should().HaveCount(2, "Editor + Management");
    }

    [Fact]
    public void Should_register_localization()
    {
        // Arrange
        var module = new ClusterManagementClientModule();

        // Act
        using var serviceProvider = module.TestModuleInitialization();
        var localizer = serviceProvider.GetRequiredService<IClientModuleLocalizer<ClusterManagementClientModule>>();

        // Assert
        Assert.NotNull(localizer);
        localizer.GetTitle().Should().NotBeNullOrEmpty();
    }
}
