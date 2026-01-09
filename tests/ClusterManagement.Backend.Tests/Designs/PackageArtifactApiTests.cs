using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using AwesomeAssertions;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Backend.Tests.Resources;
using ClusterManagement.Internal.Contracts;
using NSubstitute;
using Sdk.Backend.ArtifactApi;
using Sdk.Backend.Modules;
using Semver;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageArtifactApiTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IArtifactQueryApi _artifactApi = Substitute.For<IArtifactQueryApi>();
    private readonly IArtifactQueryBuilder _artifactQueryBuilder = Substitute.For<IArtifactQueryBuilder>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    private PackageArtifactApi SetupFunctionBlockLoader()
    {
        _fileSystem.AddDirectory("Cache");
        _workspaceProvider.Cache.Returns("Cache");

        _artifactApi.CreateQueryBuilder().Returns(_artifactQueryBuilder);
        _artifactQueryBuilder.SetupMethodReturns();

        return new(_fileSystem, _workspaceProvider, _artifactApi);
    }

    private readonly ClusterDependency _dependency = new() { Name = "ViciOne.FB1", Version = new Version(1, 0) };

    public sealed class DownloadAndExtract : PackageArtifactApiTests
    {
        [Fact]
        public async Task Should_process_single_function_block_from_http()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();

            // Act
            var result = await client.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);

            // Assert
            Assert.Null(result.Error);
            Assert.False(result.Skipped);
        }

        [Fact]
        public async Task Should_skip_existing_function_block()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var targetPath = _fileSystem.Path.Combine(_fileSystem.GetDependenciesPath(_workspaceProvider), _dependency.Name, _dependency.Version.ToString());

            var first = await client.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(targetPath, $"{_dependency.Name}.dll"));

            // Act
            var second = await client.DownloadAndExtract(_dependency.Name, _dependency.Version, CancellationToken.None);

            // Assert
            Assert.Null(first.Error);
            Assert.False(first.Skipped);
            Assert.Null(second.Error);
            Assert.True(second.Skipped);
        }
    }

    public sealed class QueryMetadataArtifacts : PackageArtifactApiTests
    {
        private readonly SemVersion _coreVersion = new(0, 48, 0);

        [Fact]
        public async Task Should_query_all_available_metadata_artifacts()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            _artifactApi.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifactItem>().Setup("0.37.0-win-x64_0.48.0.json", "fbs/ViciOne.Suite.DataPort.Anna"),
                Substitute.For<IArtifactItem>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifactItem>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                Substitute.For<IArtifactItem>().Setup("1.4.1-win-x64_0.47.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(15))
                ]);

            // Act
            var result = await client.QueryMetadataArtifacts(null);

            // Assert
            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task Should_query_all_available_embeddeD_metadata_artifacts()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var queryResult = await ResourceUtils.GetEmbeddedPackagesMetadataQueryResult();
            _artifactApi.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})",
                queryResult);

            // Act
            var result = await client.QueryMetadataArtifacts(null);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_query_all_available_metadata_artifacts_for_core_version()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            _artifactApi.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/*\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifactItem>().Setup("0.37.0-win-x64_0.48.0.json", "fbs/ViciOne.Suite.DataPort.Anna"),
                Substitute.For<IArtifactItem>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifactItem>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                ]);

            // Act
            var result = await client.QueryMetadataArtifacts(_coreVersion);

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(k => k.Name.Should().Contain("_0.48."));
        }

        [Fact]
        public async Task Should_query_package_metadata_artifacts_for_core_version()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            _artifactApi.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifactItem>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifactItem>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                ]);

            // Act
            var result = await client.QueryMetadataArtifacts(_coreVersion, "ViciOne.Driver.IoTCore");

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(k => k.Name.Should().Contain("_0.48."));
            result.Should().AllSatisfy(k => k.Path.Should().Contain("ViciOne.Driver.IoTCore"));
        }

        [Fact]
        public async Task Should_query_all_package_metadata_artifacts()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            _artifactApi.SetupExecuteQuery("items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})", [
                Substitute.For<IArtifactItem>().Setup("1.18.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore"),
                Substitute.For<IArtifactItem>().Setup("1.17.0-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                Substitute.For<IArtifactItem>().Setup("1.16.0-win-x64_0.47.0.json", "fbs/ViciOne.Driver.IoTCore", DateTimeOffset.UtcNow - TimeSpan.FromDays(15)),
                ]);

            // Act
            var result = await client.QueryMetadataArtifacts(null, "ViciOne.Driver.IoTCore");

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(k => k.Path.Should().Contain("ViciOne.Driver.IoTCore"));
        }
    }

    public class QueryZipArtifactItem : PackageArtifactApiTests
    {
        [Fact]
        public async Task Should_zip_artifact_for_metadata_artifact()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("1.18.3-win-x64_0.48.0.json", "fbs/ViciOne.Driver.IoTCore");
            var zipArtifactName = "1.18.3-win-x64.zip";
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"1.18.3-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, Substitute.For<IArtifactItem>().Setup(zipArtifactName, "enginehosts"));

            var metadata = new ArtifactMetadata { Name = "Test", Version = "1.18.3", MinCoreVersion = "0.48.0" };
            using var ms = _artifactApi.SetupMetadataDownload(metadataArtifact, metadata);

            // Act
            var result = await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

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
            var client = SetupFunctionBlockLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("1.18.x-bsi-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_downloaded()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("1.18.3-win-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_deserialized()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("1.18.3-win-x64_0.48.1.json", "fbs/ViciOne.Driver.IoTCore");
            await _artifactApi.SetupMetadataDownload(metadataArtifact, new ClusterImportResultItem());   // !no metadata!

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }
    }

    public sealed class QueryLatestZipArtifactItem : PackageArtifactApiTests
    {
        [Fact]
        public async Task Should_return_null_if_no_artifact_found_for_core_version_and_package()
        {
            // Arrange
            var client = SetupFunctionBlockLoader();
            var coreVersion = new SemVersion(0, 48, 0);
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"fbs/ViciOne.Driver.IoTCore\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, []);

            // Act
            var result = await client.QueryLatestZipArtifactItem(coreVersion, "SomePackage");

            // Assert            
            result.Should().BeNull();
        }
    }
}
