using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Tests.Tools;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Consumers;

public sealed class AddOrUpdateClusterEditorSettingsConsumerTests : TestWithDbContextSqlite<ClusterEditorDbContextSqlite>
{
    private Action<IBusRegistrationConfigurator> _configureServices;

    public AddOrUpdateClusterEditorSettingsConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<AddOrUpdateClusterEditorSettingsConsumer>();
            cfg.AddSingleton<IClusterEditorDbContext>(_ => TestDbContext);
            cfg.AddSingleton(Substitute.For<ILogger<AddOrUpdateClusterEditorSettingsConsumer>>());
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddOrUpdateClusterEditorSettings(new() { UserId = "TestUser1" });

        // Act + Assert
        await tester.TestCommand<AddOrUpdateClusterEditorSettings, AddOrUpdateClusterEditorSettingsConsumer>(command);
    }

    [Fact]
    public async Task Create_settings_should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var settings = new EditorSettings
        {
            GridMode = 2,
            PanBehavior = true,
            UserId = "TestUser1",
        };
        var command = new AddOrUpdateClusterEditorSettings(settings);

        // Act
        await tester.TestCommand<AddOrUpdateClusterEditorSettings, AddOrUpdateClusterEditorSettingsConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ErrorInfo>());
        Assert.True(await tester.Harness.Published.Any<ClusterEditorSettingsChanged>(r =>
            r.Context.Message.CrudAction == CrudAction.Created &&
            r.Context.Message.Settings?.GridMode == settings.GridMode &&
            r.Context.Message.Settings?.PanBehavior == settings.PanBehavior &&
            r.Context.Message.Settings?.UserId == settings.UserId));
    }

    [Fact]
    public async Task Update_existing_settings_should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var settings = tester.Services.GetRequiredService<IClusterEditorDbContext>().SeedSettings();
        settings.MinimapNodeColoring = true;
        var command = new AddOrUpdateClusterEditorSettings(settings);

        // Act
        await tester.TestCommand<AddOrUpdateClusterEditorSettings, AddOrUpdateClusterEditorSettingsConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ErrorInfo>());
        Assert.True(await tester.Harness.Published.Any<ClusterEditorSettingsChanged>(r =>
            r.Context.Message.CrudAction == CrudAction.Updated &&
            r.Context.Message.Settings?.MinimapNodeColoring == settings.MinimapNodeColoring &&
            r.Context.Message.Settings?.UserId == settings.UserId));
    }

    [Fact]
    public async Task Fault_consumer_should_publish_error_info()
    {
        // Arrange
        var dbContextMock = Substitute.For<IClusterEditorDbContext>();
        dbContextMock.Settings
            .Returns(ex => throw new DbUpdateException());

        _configureServices += cfg => cfg.AddSingleton(dbContextMock);

        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddOrUpdateClusterEditorSettings(new() { UserId = "TestUser1" });

        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() => tester.TestCommandFault<AddOrUpdateClusterEditorSettings, AddOrUpdateClusterEditorSettingsConsumer>(command));

        // Assert
        Assert.True(await tester.Harness.Published.Any<ErrorInfo>());
    }
}
