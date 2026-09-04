using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;
using Sdk.Testing.Backend;
using Semver;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageInfoProviderTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();
    private readonly IPackageContentProvider _packageContentProvider = Substitute.For<IPackageContentProvider>();
    private readonly IPackageArtifactRepository _repository = Substitute.For<IPackageArtifactRepository>();
    private readonly ILogger<PackageInfoProvider> _logger = Substitute.For<ILogger<PackageInfoProvider>>();

    private readonly SemVersion _coreVersion = new(0, 48, 0);
    private readonly ClusterDependency _chocolate = new()
    {
        Name = "Chocolate",
        Version = "1.0.0"
    };

    private readonly ClusterDependency _cheese = new()
    {
        Name = "Cheese",
        Version = "0.4.7"
    };

    private readonly ClusterDependency _cake = new()
    {
        Name = "Cake",
        Version = "2.1.3"
    };

    public PackageInfoProviderTests()
        => _dependencyStore.BuildStoredPackageVersionMap().Returns([]);

    public sealed class GetClusterPackageInfos : PackageInfoProviderTests
    {
        [Fact]
        public async Task Should_return_only_installed_packages_requested()
        {
            // Arrange
            var provider = CreateProvider();
            var testData = new List<ClusterDependency> { _chocolate, _cheese, _cake };

            SetupPackageContents(testData);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None, [_cheese.Name]);

            // Assert
            result.Should().OnlyContain(k => k.Name == _cheese.Name);
        }

        [Fact]
        public async Task Should_return_available_packages()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(2).And
                .AllSatisfy(k => k.InstalledVersion.Should().BeNull());
            result.Should().AllSatisfy(k => k.Versions.Should().HaveCount(1));
        }

        [Fact]
        public async Task Should_return_only_available_packages_requested()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None, [_cheese.Name]);

            // Assert
            result.Should().OnlyContain(k => k.Name == _cheese.Name);
        }

        [Fact]
        public async Task Should_return_pending_operation_packages()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            _dependencyStore.GetEnqueuedOperations(Arg.Any<CancellationToken>())
                .Returns([
                    new(new ClusterDependency { Name = "New.Block", Version = "1.0.0"}, ClusterDependencyOperationKind.Install),
                    new(new ClusterDependency { Name = _chocolate.Name, Version = "1.1.0"}, ClusterDependencyOperationKind.Install),
                    new(new ClusterDependency { Name = _cheese.Name, Version = _cheese.Version}, ClusterDependencyOperationKind.Uninstall)
                ]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(k => k.PendingOperation.Should().NotBeNull());

            result.Should().Contain(k => k.Name == "New.Block"
                && k.PendingOperation != null
                && k.PendingOperation.Dependency.Version == "1.0.0"
                && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Install);

            result.Should().Contain(k => k.Name == _chocolate.Name
                && k.PendingOperation != null
                && k.PendingOperation.Dependency.Version == "1.1.0"
                && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Install);

            result.Should().Contain(k => k.Name == _cheese.Name
                && k.PendingOperation != null
                && k.PendingOperation.Dependency.Version == _cheese.Version
                && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Uninstall);
        }

        [Fact]
        public async Task Should_return_only_operations_for_packages_requested()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            _dependencyStore.GetEnqueuedOperations(Arg.Any<CancellationToken>())
                .Returns([
                    new(new ClusterDependency { Name = "New.Block", Version = "1.0.0"}, ClusterDependencyOperationKind.Install),
                    new(new ClusterDependency { Name = _chocolate.Name, Version = "1.1.0"}, ClusterDependencyOperationKind.Install),
                    new(new ClusterDependency { Name = _cheese.Name, Version = _cheese.Version}, ClusterDependencyOperationKind.Uninstall)
                ]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None, [_cheese.Name]);

            // Assert
            result.Should().OnlyContain(k => k.Name == _cheese.Name);
        }

        [Fact]
        public async Task Should_return_local_packages()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            _dependencyStore.BuildStoredPackageVersionMap()
                .Returns(new Dictionary<string, IEnumerable<string>>
                {
                    {"Local.Package.One", ["0.9.0", "1.0.0"] },
                    {"Local.Package.Two", ["1.3.1", "1.3.5", "1.7.7"] },
                });

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(2);

            result.Should().Contain(k => k.Name == "Local.Package.One"
                && k.Versions!.Count() == 2);

            result.Should().Contain(k => k.Name == "Local.Package.Two"
                && k.Versions!.Count() == 3);
        }

        [Fact]
        public async Task Should_return_only_local_packages_requested()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());
            var requestPackage = "Local.Package.One";

            _dependencyStore.BuildStoredPackageVersionMap()
                .Returns(new Dictionary<string, IEnumerable<string>>
                {
                    {requestPackage, ["0.9.0", "1.0.0"] },
                    {"Local.Package.Two", ["1.3.1", "1.3.5", "1.7.7"] },
                });

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None, [requestPackage]);

            // Assert
            result.Should().OnlyContain(k => k.Name == requestPackage);
        }

        [Fact]
        public async Task Should_merge_local_packages()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            _dependencyStore.BuildStoredPackageVersionMap()
                .Returns(new Dictionary<string, IEnumerable<string>>
                {
                    {"Local.Package.One", ["0.9.0", "1.0.0"] },
                    {"Local.Package.Two", ["1.3.1", "1.3.5"] },
                    {"Local.Package.Three", ["2.5.3", "1.8.2"] },
                });

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(5);
        }

        [Fact]
        public async Task Should_return_active_cluster_dependencies()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var activeClusterInfo = new ClusterInfo(Guid.NewGuid(), new Version(1, 1, 0), "Default Cluster", true);

            SetupActiveClusters(activeClusterInfo, _chocolate, _cheese);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(k => k.Name == _chocolate.Name
                && k.RunningClusterVersion == _chocolate.Version.ToString());

            result.Should().Contain(k => k.Name == _cheese.Name
                && k.RunningClusterVersion == _cheese.Version.ToString());
        }

        [Fact]
        public async Task Should_merge_active_cluster_and_installed_dependencies()
        {
            // Arrange
            var provider = CreateProvider();
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
            var activeClusterInfo = new ClusterInfo(builder.Cluster.Id, builder.Cluster.Version, "Default Cluster", true);

            SetupActiveClusters(activeClusterInfo, _chocolate, _cheese);
            SetupPackageContents([_chocolate, _cheese, _cake]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(k => k.Name == _chocolate.Name
                && k.InstalledVersion == _chocolate.Version.ToString()
                && k.RunningClusterVersion == _chocolate.Version.ToString());

            result.Should().Contain(k => k.Name == _cheese.Name
                && k.InstalledVersion == _cheese.Version.ToString()
                && k.RunningClusterVersion == _cheese.Version.ToString());

            result.Should().Contain(k => k.Name == _cake.Name
                && k.InstalledVersion == _cake.Version.ToString()
                && k.RunningClusterVersion == null);
        }

        [Fact]
        public async Task Should_group_available_packages_by_name()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate, "1.0.3");
            var chocolateRe = CreateArtifact(_chocolate, "1.0.2", DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5)));

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, chocolateRe]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            var first = result.First();
            first.InstalledVersion.Should().BeNull();
            first.Versions.Should().HaveCount(2);
        }

        [Fact]
        public async Task Should_merge_installed_and_available_packages()
        {
            // Arrange
            var provider = CreateProvider();

            SetupPackageContents([_chocolate]);

            var chocolate = CreateArtifact(_chocolate, "1.0.3");
            var chocolate2 = CreateArtifact(_chocolate, "1.0.2", DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(5)));
            var chocolateOld = CreateArtifact(_chocolate, "1.0.1", DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(15)));

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([chocolate, chocolate2, chocolateOld]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            var first = result.First();
            first.InstalledVersion.Should().Be(_chocolate.Version.ToString());
            first.Versions.Should().HaveCount(3);
        }

        [Fact]
        public async Task Should_sort_packages_by_name()
        {
            // Arrange
            var provider = CreateProvider();
            var testData = new List<ClusterDependency> { _cake, _chocolate, _cheese };

            SetupPackageContents(testData);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            result.Should().HaveCount(3);
            result[0].Name.Should().Be(_cake.Name);
            result[1].Name.Should().Be(_cheese.Name);
            result[2].Name.Should().Be(_chocolate.Name);
        }

        [Fact]
        public async Task Should_mark_local_versions_with_is_local_flag()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var v100 = CreateArtifact(_chocolate, "1.0.0");
            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([v100]);

            _dependencyStore.BuildStoredPackageVersionMap()
                .Returns(new Dictionary<string, IEnumerable<string>>
                {
                    { _chocolate.Name, ["1.0.0", "1.1.0"] }
                });

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            var package = result.First(p => p.Name == _chocolate.Name);
            package.Versions.Should().Contain(v => v.Version == "1.0.0" && v.IsLocal);
            package.Versions.Should().Contain(v => v.Version == "1.1.0" && v.IsLocal);
        }

        [Fact]
        public async Task Should_mark_only_local_versions_when_mixing_artifact_and_local()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var v100 = CreateArtifact(_chocolate, "1.0.0");
            var v200 = CreateArtifact(_chocolate, "2.0.0");
            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([v100, v200]);

            _dependencyStore.BuildStoredPackageVersionMap()
                .Returns(new Dictionary<string, IEnumerable<string>>
                {
                    { _chocolate.Name, ["1.1.0"] }
                });

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            var package = result.First(p => p.Name == _chocolate.Name);
            package.Versions.Should().Contain(v => v.Version == "1.0.0" && !v.IsLocal);
            package.Versions.Should().Contain(v => v.Version == "1.1.0" && v.IsLocal);
            package.Versions.Should().Contain(v => v.Version == "2.0.0" && !v.IsLocal);
        }

        [Fact]
        public async Task Should_set_latest_update_version_to_highest_available()
        {
            // Arrange
            var provider = CreateProvider();

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>()).Returns([_chocolate]);

            var v100 = CreateArtifact(_chocolate, "1.0.0");
            var v110 = CreateArtifact(_chocolate, "1.1.0");
            var v200 = CreateArtifact(_chocolate, "2.0.0");

            _repository.QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>())
                .Returns([v100, v110, v200]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Asser            

            var package = result.First(p => p.Name == _chocolate.Name);
            package.AvailableUpdateVersion.Should().Be("2.0.0");
        }

        [Fact]
        public async Task Should_set_latest_update_version_when_no_versions_available()
        {
            // Arrange
            var provider = CreateProvider();
            SetupPackageContents([_chocolate]);

            // Act
            var result = await provider.GetClusterPackageInfos(CancellationToken.None);

            // Assert
            var package = result.First(p => p.Name == _chocolate.Name);
            package.AvailableUpdateVersion.Should().BeNull();
        }

        [Fact]
        public async Task Should_query_specific_artifacts_when_filter_packages_provided()
        {
            // Arrange
            var provider = CreateProvider();
            _packageContentProvider.GetPackageContents().Returns(new Dictionary<ClusterDependency, PackageContent>());

            var chocolate = CreateArtifact(_chocolate);
            var cheese = CreateArtifact(_cheese);

            _repository.QueryMetadataArtifacts(Arg.Any<IReadOnlyCollection<string>>(), _coreVersion, Arg.Any<CancellationToken>())
                .Returns([chocolate, cheese]);

            // Act
            await provider.GetClusterPackageInfos(CancellationToken.None, [_chocolate.Name, _cheese.Name]);

            // Assert
            await _repository.Received(1).QueryMetadataArtifacts(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<SemVersion>(), Arg.Any<CancellationToken>());
            await _repository.DidNotReceive().QueryMetadataArtifacts(Arg.Any<SemVersion>(), null, Arg.Any<CancellationToken>());
        }
    }

    public sealed class GetCoreVersion : PackageInfoProviderTests
    {
        [Fact]
        public void Should_return_core_version()
        {
            // Arrange
            var provider = CreateProvider();

            // Act
            var result = provider.GetCoreVersion();

            // Assert
            result.Should().NotBeNull();
        }
    }
    private PackageInfoProvider CreateProvider()
        => new(
            _dependencyStore,
            _snapshotStore,
            TestDbContext,
            _packageContentProvider,
            _repository,
            _logger);

    private IArtifact CreateArtifact(ClusterDependency dependency, string? versionOverride = null, DateTimeOffset? modified = null)
    {
        modified ??= DateTimeOffset.UtcNow;

        var artifact = Substitute.For<IArtifact>();
        artifact.Path.Returns($"fbs/{dependency.Name}");
        artifact.Name.Returns($"{versionOverride ?? dependency.Version}-{RuntimeInformation.RuntimeIdentifier}_{_coreVersion}.json");
        artifact.Modified.Returns(modified);
        return artifact;
    }

    private void SetupActiveClusters(ClusterInfo activeClusterInfo, params ClusterDependency[] dependencies)
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
            .Returns([activeClusterInfo, inactiveClusterInfo]);

        TestDbContext.SaveActiveClusterVersion(activeClusterBuilder.Cluster);

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
