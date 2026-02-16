using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using Semver;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class GetClusterPackageInfosConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IPackageInfoProvider _infoProvider = Substitute.For<IPackageInfoProvider>();

    public GetClusterPackageInfosConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterPackageInfosConsumer>();
            cfg.AddSingleton(_infoProvider);
            cfg.AddSingleton(Substitute.For<ILogger<GetClusterPackageInfosConsumer>>());
        };
    }

    [Fact]
    public async Task Request_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        _infoProvider.GetClusterPackageInfos(Arg.Any<CancellationToken>(), Arg.Any<List<string>?>())
            .Returns([]);
        _infoProvider.GetCoreVersion().Returns(SemVersion.Parse("1.0.0", SemVersionStyles.Strict));

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.PackageInfos.Should().BeEmpty();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_package_infos_from_provider()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var expectedPackageInfos = new List<ClusterPackageInfo>
        {
            new () {
                Name = "Package1",
                InstalledVersion = "1.0.0",
                Description = "Description1",
            },
            new () {
                Name = "Package2",
                Description = "Description2",
            }
        };
        _infoProvider.GetClusterPackageInfos(Arg.Any<CancellationToken>(), Arg.Any<List<string>?>())
            .Returns(expectedPackageInfos);
        _infoProvider.GetCoreVersion().Returns(SemVersion.Parse("1.0.0", SemVersionStyles.Strict));

        var request = new GetClusterPackageInfosRequest();

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.PackageInfos.Should().HaveCount(2);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_pass_package_names_to_provider()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var packageNames = new List<string> { "Package1", "Package2" };
        _infoProvider.GetClusterPackageInfos(Arg.Any<CancellationToken>(), Arg.Any<List<string>?>())
            .Returns(new List<ClusterPackageInfo>());
        _infoProvider.GetCoreVersion().Returns(SemVersion.Parse("1.0.0", SemVersionStyles.Strict));

        var request = new GetClusterPackageInfosRequest { PackageNames = packageNames };

        // Act
        await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        await _infoProvider.Received(1).GetClusterPackageInfos(
            Arg.Any<CancellationToken>(),
            Arg.Is<List<string>?>(names => names != null && names.SequenceEqual(packageNames)));
    }

    [Fact]
    public async Task Should_handle_exception_and_return_error_response()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var exceptionMessage = "Test error message";

        _infoProvider.GetClusterPackageInfos(Arg.Any<CancellationToken>(), Arg.Any<List<string>?>())
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));
        _infoProvider.GetCoreVersion().Returns(SemVersion.Parse("1.0.0", SemVersionStyles.Strict));

        var request = new GetClusterPackageInfosRequest();

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.PackageInfos.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be(ClusterError.UnknownError);
        response.RequestError.Message.Should().Be(exceptionMessage);
    }
}
