using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Common;
using AwesomeAssertions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.Artifacts;
using Sdk.Testing.Backend;
using Semver;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.TreeBuilder.Rules;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class GetClusterPackageInfosConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();
    private readonly IPackageContentProvider _packageContentProvider = Substitute.For<IPackageContentProvider>();
    private readonly IPackageArtifactRepository _repository = Substitute.For<IPackageArtifactRepository>();

    private readonly SemVersion _coreVersion = new(0, 48, 0);
    private readonly ClusterDependency _chocolate = new()
    {
        Name = "Chocolate",
        Version = new Version(1, 0, 0)
    };

    private readonly ClusterDependency _cheese = new()
    {
        Name = "Cheese",
        Version = new Version(0, 4, 7)
    };

    private readonly ClusterDependency _cake = new()
    {
        Name = "Cake",
        Version = new Version(2, 1, 3)
    };

    public GetClusterPackageInfosConsumerTests()
    {
        _dependencyStore.BuildStoredPackageVersionMap()
            .Returns([]);

        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterPackageInfosConsumer>();
            cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_dependencyStore);
            cfg.AddSingleton(_packageContentProvider);
            cfg.AddSingleton(_repository);
            cfg.AddSingleton(_snapshotStore);
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
            _chocolate,
            _cheese,
            _cake
        };

        SetupPackageContents(testData);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(testData.Count).And
            .AllSatisfy(k => k.InstalledVersion.Should().NotBeNull());

        response.PackageInfos.Should().AllSatisfy(k => k.PendingOperation.Should().BeNull());
        response.PackageInfos.Should().AllSatisfy(k => k.PendingOperation.Should().BeNull());
    }

    [Fact]
    public async Task Should_return_only_installed_packages_requested()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest([_cheese.Name]);

        var testData = new List<ClusterDependency>
        {
            _chocolate,
            _cheese,
            _cake
        };

        SetupPackageContents(testData);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().OnlyContain(k => k.Name == _cheese.Name);
    }

    [Fact]
    public async Task Request_should_return_available_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = CreateArtifact(_chocolate);
        var cheese = CreateArtifact(_cheese);

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
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
    public async Task Should_return_only_available_packages_requested()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest([_cheese.Name]);

        var chocolate = CreateArtifact(_chocolate);
        var cheese = CreateArtifact(_cheese);

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, cheese]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().OnlyContain(k => k.Name == _cheese.Name);
    }

    [Fact]
    public async Task Request_should_return_pending_operation_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = CreateArtifact(_chocolate);
        var cheese = CreateArtifact(_cheese);

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, cheese]);

        _dependencyStore.GetEnqueuedOperations(Arg.Any<CancellationToken>())
            .Returns([
                new(new ClusterDependency { Name = "New.Block", Version = new Version(1,0,0)}, ClusterDependencyOperationKind.Install),
                new(new ClusterDependency { Name = _chocolate.Name, Version = new Version(1,1,0)}, ClusterDependencyOperationKind.Install),
                new(new ClusterDependency { Name = _cheese.Name, Version = _cheese.Version}, ClusterDependencyOperationKind.Uninstall)
            ]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(3);

        response.PackageInfos.Should().AllSatisfy(k => k.PendingOperation.Should().NotBeNull());
        response.PackageInfos.Should().Contain(k => k.Name == "New.Block"
            && k.PendingOperation != null
            && k.PendingOperation.Dependency.Version == new Version(1, 0, 0)
            && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Install);

        response.PackageInfos.Should().Contain(k => k.Name == _chocolate.Name
            && k.PendingOperation != null
            && k.PendingOperation.Dependency.Version == new Version(1, 1, 0)
            && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Install);

        response.PackageInfos.Should().Contain(k => k.Name == _cheese.Name
            && k.PendingOperation != null
            && k.PendingOperation.Dependency.Version == _cheese.Version
            && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Uninstall);
    }

    [Fact]
    public async Task Should_return_only_operations_for_packages_requested()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest([_cheese.Name]);

        var chocolate = CreateArtifact(_chocolate);
        var cheese = CreateArtifact(_cheese);

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, cheese]);

        _dependencyStore.GetEnqueuedOperations(Arg.Any<CancellationToken>())
            .Returns([
                new(new ClusterDependency { Name = "New.Block", Version = new Version(1,0,0)}, ClusterDependencyOperationKind.Install),
                new(new ClusterDependency { Name = _chocolate.Name, Version = new Version(1,1,0)}, ClusterDependencyOperationKind.Install),
                new(new ClusterDependency { Name = _cheese.Name, Version = _cheese.Version}, ClusterDependencyOperationKind.Uninstall)
            ]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().OnlyContain(k => k.Name == _cheese.Name);
    }

    [Fact]
    public async Task Request_should_return_local_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        _dependencyStore.BuildStoredPackageVersionMap()
            .Returns(new Dictionary<string, IEnumerable<Version>>
            {
                {"Local.Package.One", [new Version(0,9,0), new Version(1,0,0)] },
                {"Local.Package.Two", [new Version(1,3,1), new Version(1,3,5), new Version(1,7,7)] },
            });

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(2);

        response.PackageInfos.Should().Contain(k => k.Name == "Local.Package.One"
            && k.Versions!.Count() == 2);

        response.PackageInfos.Should().Contain(k => k.Name == "Local.Package.Two"
            && k.Versions!.Count() == 3);
    }

    [Fact]
    public async Task Should_return_only_local_packages_requested()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var requestPackage = "Local.Package.One";
        var request = new GetClusterPackageInfosRequest([requestPackage]);

        _dependencyStore.BuildStoredPackageVersionMap()
            .Returns(new Dictionary<string, IEnumerable<Version>>
            {
                {requestPackage, [new Version(0,9,0), new Version(1,0,0)] },
                {"Local.Package.Two", [new Version(1,3,1), new Version(1,3,5), new Version(1,7,7)] },
            });

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().OnlyContain(k => k.Name == requestPackage);
    }

    [Fact]
    public async Task Request_should_merge_local_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = CreateArtifact(_chocolate);
        var cheese = CreateArtifact(_cheese);

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, cheese]);

        _dependencyStore.BuildStoredPackageVersionMap()
            .Returns(new Dictionary<string, IEnumerable<Version>>
            {
                {"Local.Package.One", [new Version(0,9,0), new Version(1,0,0)] },
                {"Local.Package.Two", [new Version(1,3,1), new Version(1,3,5)] },
                {"Local.Package.Three", [new Version(2,5,3), new Version(1,8,2)] },
            });

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(5);
    }

    [Fact]
    public async Task Request_should_return_active_cluster_dependencies()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var activeClusterInfo = new ClusterInfo(Guid.NewGuid(), new Version(1, 1, 0), "Default Cluster", true);
        var inactiveClusterInfo = new ClusterInfo(activeClusterInfo.Id, new Version(0, 5, 0), "Old Default Cluster", false);

        SetupActiveClusters(tester.Services.GetRequiredService<IClusterDbContext>(), activeClusterInfo, _chocolate, _cheese);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(2);
        response.PackageInfos.Should().Contain(k => k.Name == _chocolate.Name
            && k.RunningClusterVersion == _chocolate.Version.ToString());

        response.PackageInfos.Should().Contain(k => k.Name == _cheese.Name
            && k.RunningClusterVersion == _cheese.Version.ToString());
    }

    [Fact]
    public async Task Request_should_merge_active_cluster_and_installed_dependencies()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        var request = new GetClusterPackageInfosRequest();
        var activeClusterInfo = new ClusterInfo(builder.Cluster.Id, builder.Cluster.Version, "Default Cluster", true);

        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();

        SetupActiveClusters(dbContext, activeClusterInfo, _chocolate, _cheese);

        SetupPackageContents([_chocolate, _cheese, _cake]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(3);
        response.PackageInfos.Should().Contain(k => k.Name == _chocolate.Name
            && k.InstalledVersion == _chocolate.Version.ToString()
            && k.RunningClusterVersion == _chocolate.Version.ToString());

        response.PackageInfos.Should().Contain(k => k.Name == _cheese.Name
            && k.InstalledVersion == _cheese.Version.ToString()
            && k.RunningClusterVersion == _cheese.Version.ToString());

        response.PackageInfos.Should().Contain(k => k.Name == _cake.Name
            && k.InstalledVersion == _cake.Version.ToString()
            && k.RunningClusterVersion == null);
    }

    [Fact]
    public async Task Request_should_group_available_packages_by_name()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        var chocolate = CreateArtifact(_chocolate, new Version(1, 0, 3));
        var chocolateRe = CreateArtifact(_chocolate, new Version(1, 0, 2), DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5)));

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, chocolateRe]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.PackageInfos.Should().HaveCount(1);
        var first = response.PackageInfos.First();
        first.InstalledVersion.Should().BeNull();
        response.PackageInfos.First().Versions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Request_should_merge_installed_and_available_packages()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        SetupPackageContents([_chocolate]);

        var chocolate = CreateArtifact(_chocolate, new Version(1, 0, 3));
        var chocolate2 = CreateArtifact(_chocolate, new Version(1, 0, 2), DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5)));
        var chocolateOld = CreateArtifact(_chocolate, new Version(1, 0, 1), DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(15)));

        _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
            .Returns([chocolate, chocolate2, chocolateOld]);

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        var first = response.PackageInfos.First();
        first.InstalledVersion.Should().Be(_chocolate.Version.ToString());
        response.PackageInfos.First().Versions.Should().HaveCount(3);
    }

    [Fact]
    public async Task Request_should_return_error_on_failure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterPackageInfosRequest();

        _packageContentProvider.GetPackageContents().Throws<InvalidOperationException>();

        // Act
        var response = await tester.TestRequest<GetClusterPackageInfosResponse, GetClusterPackageInfosRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.PackageInfos.Should().BeEmpty();
    }

    private IArtifact CreateArtifact(ClusterDependency dependency, Version? versionOverride = null, DateTimeOffset? modified = null)
    {
        modified ??= DateTimeOffset.UtcNow;

        var artifact = Substitute.For<IArtifact>();
        artifact.Path.Returns($"fbs/{dependency.Name}");
        artifact.Name.Returns($"{versionOverride ?? dependency.Version}-{RuntimeInformation.RuntimeIdentifier}_{_coreVersion}.json");
        artifact.Modified.Returns(modified);
        return artifact;
    }

    private void SetupActiveClusters(IClusterDbContext dbContext, ClusterInfo activeClusterInfo, params ClusterDependency[] dependencies)
    {
        using var activeClusterBuilder = ClusterFactory.CreateComplexCluster();
        activeClusterBuilder.Cluster.Id = activeClusterInfo.Id;
        activeClusterBuilder.Cluster.Version = activeClusterInfo.Version;
        activeClusterBuilder.Cluster.Dependencies.Clear();
        activeClusterBuilder.Cluster.Dependencies.AddRange(dependencies);

        var activeClusterJson = ClusterSerializer.Serialize(activeClusterBuilder.Cluster);
        var jsonNode = JsonSerializer.Deserialize<JsonNode>(activeClusterJson, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions);

        var inactiveClusterInfo = new ClusterInfo(activeClusterInfo.Id, new Version(0, 5, 0), "Old Default Cluster", false);

        _snapshotStore.GetClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns(
            [
                activeClusterInfo,
                inactiveClusterInfo
            ]);

        dbContext.SaveActiveClusterVersion(activeClusterBuilder.Cluster);

        _snapshotStore.Load(activeClusterInfo.Id, activeClusterInfo.Version, Arg.Any<Func<Stream, CancellationToken, Task<JsonNode>>>(), Arg.Any<CancellationToken>())
            .Returns(jsonNode);
    }


    private void SetupPackageContents(List<ClusterDependency> dependencies)
    {
        var contents = dependencies.Select(k => new KeyValuePair<ClusterDependency, PackageContent>(
            k,
            new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset>(),
                false
            )
        )).ToDictionary();

        _packageContentProvider.GetPackageContents()
            .Returns(contents);
    }
}
