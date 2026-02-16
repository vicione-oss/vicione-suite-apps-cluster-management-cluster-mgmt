using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Backend.Tests.Resources;
using ClusterManagement.Internal.Contracts;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageArtifactRepositoryTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IArtifactRepository _repository = Substitute.For<IArtifactRepository>();
    private readonly IArtifactQueryBuilder _artifactQueryBuilder = Substitute.For<IArtifactQueryBuilder>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    private PackageArtifactRepository SetupPackageArtifactRepository()
    {
        _fileSystem.AddDirectory("Cache");
        _workspaceProvider.Cache.Returns("Cache");

        _repository.CreateQueryBuilder().Returns(_artifactQueryBuilder);
        _artifactQueryBuilder.SetupMethodReturns();

        return new(_fileSystem, _workspaceProvider, _repository);
    }

    private readonly ClusterDependency _dependency = new() { Name = "ViciOne.FB1", Version = new Version(1, 0) };

    public sealed class DownloadAndExtract : PackageArtifactRepositoryTests
    {
        private void SetupPackageArtifact(string path, string packageName)
        {
            var artifact = Substitute.For<IArtifact>().Setup(packageName, path);
            var queryResult = Substitute.For<IArtifactQueryResult>();
            queryResult.Artifacts.Returns([artifact]);

            _repository.Query(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(queryResult);
        }

        [Fact]
        public async Task Should_process_single_function_block_from_http()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();

            SetupPackageArtifact($"fbs/{_dependency.Name}", $"{_dependency.Version}.zip");

            // Act
            var result = await packageRepo.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);

            // Assert
            Assert.Null(result.Error);
            Assert.False(result.Skipped);
        }

        [Fact]
        public async Task Should_skip_existing_function_block()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var targetPath = _fileSystem.Path.Combine(_fileSystem.GetDependenciesPath(_workspaceProvider), _dependency.Name, _dependency.Version.ToString());

            SetupPackageArtifact($"fbs/{_dependency.Name}", $"{_dependency.Version}.zip");

            var first = await packageRepo.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(targetPath, $"{_dependency.Name}.dll"));

            // Act
            var second = await packageRepo.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);

            // Assert
            Assert.Null(first.Error);
            Assert.False(first.Skipped);
            Assert.Null(second.Error);
            Assert.True(second.Skipped);
        }
    }

    public sealed class QueryMetadataArtifacts : PackageArtifactRepositoryTests
    {
        private readonly SemVersion _coreVersion = new(0, 48, 0);

        [Fact]
        public async Task Should_query_all_available_metadata_artifacts()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            _repository.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifact>().Setup("0.37.0-win-x64_0.48.0.json", "fbs/ViciOne.Suite.DataPort.Anna"),
                Substitute.For<IArtifact>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifact>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                Substitute.For<IArtifact>().Setup("1.4.1-win-x64_0.47.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(15))
                ]);

            // Act
            var result = await packageRepo.QueryMetadataArtifacts(null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task Should_query_all_available_embedded_metadata_artifacts()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var queryResult = await ResourceUtils.GetEmbeddedPackagesMetadataQueryResult(TestContext.Current.CancellationToken);
            _repository.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})",
                queryResult);

            // Act
            var result = await packageRepo.QueryMetadataArtifacts(null, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_query_all_available_metadata_artifacts_for_core_version()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            _repository.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifact>().Setup("0.37.0-win-x64_0.48.0.json", "fbs/ViciOne.Suite.DataPort.Anna"),
                Substitute.For<IArtifact>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifact>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                ]);

            // Act
            var result = await packageRepo.QueryMetadataArtifacts(_coreVersion, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(k => k.Name.Should().Contain("_0.48."));
        }

        [Fact]
        public async Task Should_query_package_metadata_artifacts_for_core_version()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            _repository.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifact>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifact>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                ]);

            // Act
            var result = await packageRepo.QueryMetadataArtifacts(_coreVersion, "ViciOne.Driver.IoTCore", TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(k => k.Name.Should().Contain("_0.48."));
            result.Should().AllSatisfy(k => k.Path.Should().Contain("ViciOne.Driver.IoTCore"));
        }

        [Fact]
        public async Task Should_query_all_package_metadata_artifacts()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            _repository.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifact>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifact>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                Substitute.For<IArtifact>().Setup("1.16.0-win-x64_0.47.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(15)),
                ]);

            // Act
            var result = await packageRepo.QueryMetadataArtifacts(null, "ViciOne.Driver.IoTCore", TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(k => k.Path.Should().Contain("ViciOne.Driver.IoTCore"));
        }
    }

    public class QueryZipArtifact : PackageArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_zip_artifact_for_metadata_artifact()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var metadataArtifact = Substitute.For<IArtifact>().Setup("1.18.3-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore");
            var zipArtifactName = "1.18.3-win-x64.zip";
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"1.18.3-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _repository.SetupExecuteQuery(query, Substitute.For<IArtifact>().Setup(zipArtifactName, "enginehosts"));

            var metadata = new ArtifactMetadata { Name = "Test", Version = "1.18.3", MinCoreVersion = "0.48.0" };
            using var ms = _repository.SetupMetadataDownload(metadataArtifact, metadata);

            // Act
            var result = await packageRepo.QueryZipArtifact(metadataArtifact, CancellationToken.None);

            // Assert
            _artifactQueryBuilder.AndPathMatches("enginehosts").Received(1);
            _artifactQueryBuilder.AndNameMatches(Arg.Is<string>(p => p == zipArtifactName)).Received(1);

            result.Should().NotBeNull();
            result.Metadata.Should().BeEquivalentTo(metadata);
            result.Name.Should().Be(zipArtifactName);
        }

        [Fact]
        public async Task Should_throw_on_invalid_artifact_name()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var metadataArtifact = Substitute.For<IArtifact>().Setup("1.18.x-bsi-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");

            // Act
            var act = async () => await packageRepo.QueryZipArtifact(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_downloaded()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var metadataArtifact = Substitute.For<IArtifact>().Setup("1.18.3-win-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");

            // Act
            var act = async () => await packageRepo.QueryZipArtifact(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_deserialized()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var metadataArtifact = Substitute.For<IArtifact>().Setup("1.18.3-win-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");
            await _repository.SetupMetadataDownload(metadataArtifact, new ClusterImportResultItem());   // !no metadata!

            // Act
            var act = async () => await packageRepo.QueryZipArtifact(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }
    }

    public sealed class QueryLatestZipArtifact : PackageArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_return_null_if_no_artifact_found_for_core_version_and_package()
        {
            // Arrange
            var packageRepo = SetupPackageArtifactRepository();
            var coreVersion = new SemVersion(0, 48, 0);
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _repository.SetupExecuteQuery(query, []);

            // Act
            var result = await packageRepo.QueryLatestZipArtifact(coreVersion, "SomePackage", TestContext.Current.CancellationToken);

            // Assert            
            result.Should().BeNull();
        }
    }
}
