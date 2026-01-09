using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Designs;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.ArtifactApi;
using Sdk.Testing.Backend;
using Semver;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class GetClusterPackagesConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IDesignProvider _designProvider = Substitute.For<IDesignProvider>();
    private readonly IPackageArtifactApi _packageApi = Substitute.For<IPackageArtifactApi>();

    public GetClusterPackagesConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterPackageInfosConsumer>();
            cfg.AddSingleton(_designProvider);
            cfg.AddSingleton(_packageApi);
        };
    }

    [Fact]
    public async Task Request_should_return_no_packages_if_none_are_installed_or_available()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_return_installed_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var testData = new List<ClusterDependency>
        {
            new()
            {
                Name = "Chocolate",
                Version = new Version(1, 0, 0)
            },
            new()
            {
                Name = "Cheese",
                Version = new Version(0, 4, 7)
            },
            new()
            {
                Name = "Cake",
                Version = new Version(2, 1, 3)
            },
        };

        _designProvider.GetClusterDependencies()
            .Returns(testData);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(testData.Count).And
            .AllSatisfy(k => k.InstalledVersion.Should().NotBeNull());
    }

    [Fact]
    public async Task Request_should_return_available_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = Substitute.For<IArtifactItem>();
        chocolate.Path = "fbs/Chocolate";
        chocolate.Name = "1.0.2-win-x64_0.48.0.json";
        chocolate.Modified = DateTimeOffset.UtcNow;

        var cheese = Substitute.For<IArtifactItem>();
        cheese.Path = "fbs/Cheese";
        cheese.Name = "0.4.8-win-x64_0.48.0.json";
        chocolate.Modified = DateTimeOffset.UtcNow;

        _packageApi.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, cheese]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(2).And
            .AllSatisfy(k => k.InstalledVersion.Should().BeNull());

        response.PackageInfos.Should().AllSatisfy(k => k.Versions.Should().HaveCount(1));
    }

    [Fact]
    public async Task Request_should_group_available_packages_by_name()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = Substitute.For<IArtifactItem>();
        chocolate.Path = "fbs/Chocolate";
        chocolate.Name = "1.0.3-win-x64_0.48.0.json";
        chocolate.Modified = DateTimeOffset.UtcNow;

        var chocolateRe = Substitute.For<IArtifactItem>();
        chocolateRe.Path = "fbs/Chocolate";
        chocolateRe.Name = "1.0.2-win-x64_0.48.0.json";
        chocolateRe.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5));

        var chocolateCi = Substitute.For<IArtifactItem>();
        chocolateCi.Path = "fbs/Chocolate";
        chocolateCi.Name = "1.0.2-ci342342-win-x64_0.48.0.json";
        chocolateCi.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(7));

        _packageApi.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, chocolateRe, chocolateCi]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(1);
        var first = response.PackageInfos.First();
        first.InstalledVersion.Should().BeNull();
        response.PackageInfos.First().Versions.Should().HaveCount(3);
    }

    [Fact]
    public async Task Request_should_merge_installed_and_available_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var installedChocolate = new ClusterDependency()
        {
            Name = "Chocolate",
            Version = new Version(1, 0, 0)
        };

        _designProvider.GetClusterDependencies()
            .Returns([installedChocolate]);


        var chocolate = Substitute.For<IArtifactItem>();
        chocolate.Path = "fbs/Chocolate";
        chocolate.Name = "1.0.3-win-x64_0.48.0.json";
        chocolate.Modified = DateTimeOffset.UtcNow;

        var chocolate2 = Substitute.For<IArtifactItem>();
        chocolate2.Path = "fbs/Chocolate";
        chocolate2.Name = "1.0.2-win-x64_0.48.0.json";
        chocolate2.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5));

        var chocolateCi = Substitute.For<IArtifactItem>();
        chocolateCi.Path = "fbs/Chocolate";
        chocolateCi.Name = "1.0.2-ci342342-win-x64_0.48.0.json";
        chocolateCi.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(7));

        var chocolateOld = Substitute.For<IArtifactItem>();
        chocolateOld.Path = "fbs/Chocolate";
        chocolateOld.Name = "1.0.1-win-x64_0.48.0.json";
        chocolateOld.Modified = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(15));

        _packageApi.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, chocolate2, chocolateCi, chocolateOld]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        var first = response.PackageInfos.First();
        first.InstalledVersion.Should().Be("1.0.0");
        response.PackageInfos.First().Versions.Should().HaveCount(4);
    }

    [Fact]
    public async Task Request_should_return_error_on_failure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        _designProvider.GetClusterDependencies(Arg.Any<bool>()).ThrowsAsync<InvalidOperationException>();

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.PackageInfos.Should().BeEmpty();
    }
}
