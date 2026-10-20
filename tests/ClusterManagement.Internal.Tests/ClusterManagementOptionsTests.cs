using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Extensions;
using Sdk.Testing.Backend;

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

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    public void Deploy_saga_timeouts_should_be_positive(string timeout)
    {
        // Arrange: AddModuleWithOptions only sets the top level options
        var config = new TestConfig()
            .AddModuleWithOptions(_moduleId, _options);
        config.SetSetting("ViciOneSuiteClusterManagement:DeploySagaTimeouts:UpdatingDeployments", timeout);

        var services = new ServiceCollection()
            .AddConfiguration(config)
            .AddModuleSection<ClusterManagementOptions>(_moduleId);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var act = () => serviceProvider.GetRequiredService<IOptions<ClusterManagementOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain(nameof(DeploySagaTimeoutOptions.UpdatingDeployments));
    }
}
