using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Internal.Tests;

public class ClusterManagementOptionsTests
{
    private readonly string _moduleId = "ViciOne.Suite.ClusterManagement";

    private readonly ClusterManagementOptions _options = new();

    [Fact]
    public void Settings_should_be_available_from_configuration()
    {
        // Arrange
        var config = new TestConfig()
            .AddModuleWithOptions(_moduleId, _options)
            .BuildConfiguration();

        // Act
        var options = config.BindSection<ClusterManagementOptions>(_moduleId.Replace(".", "", StringComparison.Ordinal));

        // Assert
        Assert.NotNull(options);
        options.Should().BeEquivalentTo(_options);
    }

    [Fact]
    public void Settings_should_be_available_from_service_provider()
    {
        // Arrange
        var config = new TestConfig()
            .AddModuleWithOptions(_moduleId, _options);

        var services = new ServiceCollection()
            .AddConfiguration(config)
            .AddModuleSection<ClusterManagementOptions>(_moduleId);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<ClusterManagementOptions>>();

        // Assert
        Assert.NotNull(options);
        options.Value.Should().BeEquivalentTo(_options);
    }
}
