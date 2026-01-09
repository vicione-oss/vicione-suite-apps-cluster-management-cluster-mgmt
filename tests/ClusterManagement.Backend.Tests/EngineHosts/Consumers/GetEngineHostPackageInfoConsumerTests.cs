using AwesomeAssertions;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.ArtifactApi;
using Sdk.Testing.Backend;
using Semver;
using Xunit;

namespace ClusterManagement.Backend.Tests.EngineHosts.Consumers;

public class GetEngineHostPackageInfoConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly ClusterManagementOptions _options = new() { EngineHostVersion = "0.28.0" };
    private readonly IEngineHostArtifactApi _packageApi = Substitute.For<IEngineHostArtifactApi>();

    public GetEngineHostPackageInfoConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetEngineHostPackageInfoConsumer>();
            cfg.AddSingleton(Options.Create(_options));
            cfg.AddSingleton(_packageApi);
        };
    }

    [Fact]
    public async Task Request_should_return_no_package_versions_if_none_available()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetEngineHostPackageInfoRequest();

        // Act
        var response = await tester.TestRequest<GetEngineHostPackageInfoResponse, GetEngineHostPackageInfoRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfo.Should().NotBeNull();
        response.PackageInfo.Versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_return_installed_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetEngineHostPackageInfoRequest();

        var version1 = Substitute.For<IArtifactItem>();
        version1.Path = "enginehost";
        version1.Name = "0.28.0-win-x64_0.48.0.json";
        version1.Modified = DateTimeOffset.UtcNow;

        var version1ci = Substitute.For<IArtifactItem>();
        version1ci.Path = "enginehost";
        version1ci.Name = "0.28.0-ci234343-win-x64_0.48.0.json";
        version1ci.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(2));

        var version2 = Substitute.For<IArtifactItem>();
        version2.Path = "enginehost";
        version2.Name = "0.27.0-win-x64_0.48.0.json";
        version1.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5));

        _packageApi.QueryMetadataArtifacts(Arg.Any<SemVersion>(), Arg.Any<CancellationToken>())
            .Returns([version1, version1ci, version2]);

        // Act
        var response = await tester.TestRequest<GetEngineHostPackageInfoResponse, GetEngineHostPackageInfoRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfo.Should().NotBeNull();
        response.PackageInfo.InstalledVersion.Should().Be(_options.EngineHostVersion);
        response.PackageInfo.Versions.Should().HaveCount(3);
    }
}
