using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Sdk.Testing.Backend;
using Semver;

namespace ClusterManagement.Backend.Tests.EngineHosts.Consumers;

public class GetEngineHostPackageInfoConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IEngineHostArtifactRepository _repository = Substitute.For<IEngineHostArtifactRepository>();

    public GetEngineHostPackageInfoConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetEngineHostPackageInfoConsumer>();
            cfg.AddSingleton(Options.Create(new ClusterManagementOptions()));
            cfg.AddSingleton(_repository);
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

        var version1 = Substitute.For<IArtifact>();
        version1.Path.Returns("enginehost");
        version1.Name.Returns("0.28.0-win-x64_0.48.0.json");
        version1.Modified.Returns(DateTimeOffset.UtcNow);

        var version1ci = Substitute.For<IArtifact>();
        version1ci.Path.Returns("enginehost");
        version1ci.Name.Returns("0.28.0-ci234343-win-x64_0.48.0.json");
        version1ci.Modified.Returns(DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(2)));

        var version2 = Substitute.For<IArtifact>();
        version2.Path.Returns("enginehost");
        version2.Name.Returns("0.27.0-win-x64_0.48.0.json");
        version1.Modified.Returns(DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5)));

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), Arg.Any<CancellationToken>())
            .Returns([version1, version1ci, version2]);

        // Act
        var response = await tester.TestRequest<GetEngineHostPackageInfoResponse, GetEngineHostPackageInfoRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfo.Should().NotBeNull();
        response.PackageInfo.InstalledVersion.Should().Be("0.30.0");
        response.PackageInfo.Versions.Should().HaveCount(3);
    }
}
