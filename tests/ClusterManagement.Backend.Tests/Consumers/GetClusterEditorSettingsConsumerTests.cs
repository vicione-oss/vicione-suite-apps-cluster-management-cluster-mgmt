using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Tests.Tools;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;
using ClusterManagement.Internal.Requests;

namespace ClusterManagement.Backend.Tests.Consumers;

public sealed class GetClusterEditorSettingsConsumerTests : TestWithDbContextSqlite<ClusterEditorDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetClusterEditorSettingsConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterEditorSettingsConsumer>();
            cfg.AddSingleton<IClusterEditorDbContext>(_ => TestDbContext);
            cfg.AddSingleton(Substitute.For<ILogger<GetClusterEditorSettingsConsumer>>());
        };

    [Fact]
    public async Task Request_unknown_userId_should_return_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterEditorSettingsRequest("Anonymous");

        // Act
        var response = await tester.TestRequest<GetClusterEditorSettingsResponse, GetClusterEditorSettingsRequest>(request);

        // Assert
        response.Settings.Should().BeNull();
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Request_existing_userId_should_return_settings()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IClusterEditorDbContext>();
        var settings = dbContext.SeedSettings();
        var request = new GetClusterEditorSettingsRequest(settings.UserId);

        // Act
        var response = await tester.TestRequest<GetClusterEditorSettingsResponse, GetClusterEditorSettingsRequest>(request);

        // Assert
        response.Settings.Should().BeEquivalentTo(settings);
        response.RequestError.Should().BeNull();
    }
}
