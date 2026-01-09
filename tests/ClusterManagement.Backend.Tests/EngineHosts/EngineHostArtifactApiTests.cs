using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using System.Text.Json;
using AwesomeAssertions;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Backend.Tests.Resources;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Tests.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.ArtifactApi;
using Sdk.Backend.Modules;
using Semver;
using Xunit;

namespace ClusterManagement.Backend.Tests.EngineHosts;

public class EngineHostArtifactApiTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IArtifactQueryApi _artifactApi = Substitute.For<IArtifactQueryApi>();
    private readonly IArtifactQueryBuilder _artifactQueryBuilder = Substitute.For<IArtifactQueryBuilder>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IOptions<ClusterManagementOptions> _options = Substitute.For<IOptions<ClusterManagementOptions>>();

    private EngineHostArtifactApi SetupEngineHostLoader()
    {
        _fileSystem.AddDirectory("Cache");
        _workspaceProvider.Cache.Returns("Cache");
        _artifactApi.CreateQueryBuilder().Returns(_artifactQueryBuilder);
        _options.Value.Returns(ClusterManagementTools.ClusterManagementOptions);
        _artifactQueryBuilder.SetupMethodReturns();

        return new(_fileSystem, _workspaceProvider, _artifactApi, _options);
    }

    public class DownloadAndExtract : EngineHostArtifactApiTests
    {
        [Fact]
        public async Task Should_process_engine_host_from_http()
        {
            // Arrange
            var client = SetupEngineHostLoader();

            // Act
            var result = await client.DownloadAndExtract(CancellationToken.None);

            // Assert
            result.Error.Should().BeNull();
            result.Skipped.Should().BeFalse();
        }

        [Fact]
        public async Task Should_skip_existing_engine_host_if_overwrite_not_set()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var packageName = _options.Value.GetEngineHostPackageName();
            var engineHostVersionPath = _fileSystem.GetEngineHostVersionsPath(_workspaceProvider);
            var targetDirectory = _fileSystem.Path.Combine(engineHostVersionPath, packageName);

            var first = await client.DownloadAndExtract(CancellationToken.None);
            _fileSystem.AddEmptyFile(_fileSystem.Path.Combine(targetDirectory, "content"));

            // Act
            var second = await client.DownloadAndExtract(CancellationToken.None);

            // Assert
            first.Error.Should().BeNull();
            first.Skipped.Should().BeFalse();
            second.Error.Should().BeNull();
            second.Skipped.Should().BeTrue();
        }
    }

    public class QueryMetadataArtifacts : EngineHostArtifactApiTests
    {
        [Fact]
        public async Task Should_load_all_metadata_artifacts()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, [
                Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts", DateTimeOffset.UtcNow),
                Substitute.For<IArtifactItem>().Setup("0.27.0-linux-x64_0.48.0.json", "enginehosts", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                Substitute.For<IArtifactItem>().Setup("0.26.0-linux-x64_0.47.0.json", "enginehosts", DateTimeOffset.UtcNow - TimeSpan.FromDays(15))
                ]);

            // Act
            var result = await client.QueryMetadataArtifacts(null, CancellationToken.None);

            // Assert
            _artifactQueryBuilder.AndPathMatches("enginehosts").Received(1);
            _artifactQueryBuilder.AndNameMatches(Arg.Is<string>(p => p.Contains(RuntimeInformation.RuntimeIdentifier, StringComparison.Ordinal))).Received(1);
            _artifactQueryBuilder.OrderByDescending("updated").Received(1);
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task Should_load_all_embedded_metadata_artifacts()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";

            var queryResult = await ResourceUtils.GetEmbeddedEngineHostMetadataQueryResult();
            _artifactApi.SetupExecuteQuery(query, queryResult);

            // Act
            var result = await client.QueryMetadataArtifacts(null, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_return_metadata_for_given_core_version()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var coreVersion = new SemVersion(0, 48, 0);
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";

            _artifactApi.SetupExecuteQuery(query, Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts", DateTimeOffset.UtcNow));

            // Act
            var result = await client.QueryMetadataArtifacts(coreVersion, CancellationToken.None);

            // Assert
            _artifactQueryBuilder.AndPathMatches("enginehosts").Received(1);
            _artifactQueryBuilder.AndNameMatches(Arg.Is<string>(p =>
                p.Contains(RuntimeInformation.RuntimeIdentifier, StringComparison.Ordinal) &&
                p.Contains($"{coreVersion.Major}.{coreVersion.Minor}"))).Received(1);
            _artifactQueryBuilder.OrderByDescending("updated").Received(1);
            result.Should().HaveCount(1);
        }
    }

    public class QueryLatestZipArtifactItem : EngineHostArtifactApiTests
    {
        private readonly SemVersion _coreVersion = new(0, 48, 0);

        [Fact]
        public async Task Should_load_latest_metadata_artifact_for_core_version()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var expectedPackageName = "0.28.0-linux-x64.zip";
            var metadataQuery = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            var latestArtifact = Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts", DateTimeOffset.UtcNow);

            // Because there's a second query for engine host artifact we need to setup another query builder
            IArtifactItem[] ehArtifacts = [Substitute.For<IArtifactItem>().Setup(expectedPackageName, "SomePath", DateTimeOffset.UtcNow)];
            var engineHostQueryBuilder = Substitute.For<IArtifactQueryBuilder>();
            var engineHostQuery = "eh-artifact-query";
            engineHostQueryBuilder.BuildQueryString().Returns(engineHostQuery);
            var engineHostQueryResult = Substitute.For<IArtifactQueryResult>();
            engineHostQueryResult.Artifacts.Returns(ehArtifacts.AsReadOnly());

            _artifactQueryBuilder.AndNameMatches(expectedPackageName).Returns(engineHostQueryBuilder);
            _artifactApi.ExecuteQuery(engineHostQuery, Arg.Any<CancellationToken>())
                .Returns(engineHostQueryResult);

            _artifactApi.SetupExecuteQuery(metadataQuery, latestArtifact);

            var metadata = new ArtifactMetadata { Name = "Test", Version = "0.28.0", MinCoreVersion = "0.48.0" };
            using var ms = _artifactApi.SetupMetadataDownload(latestArtifact, metadata);

            // Act
            var result = await client.QueryLatestZipArtifactItem(_coreVersion, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(expectedPackageName);
            result.Metadata.Should().BeEquivalentTo(metadata);
        }

        [Fact]
        public async Task Should_return_null_if_no_artifact_found_for_core_version()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var coreVersion = new SemVersion(0, 48, 0);
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, []);

            // Act
            var result = await client.QueryLatestZipArtifactItem(coreVersion, CancellationToken.None);

            // Assert            
            result.Should().BeNull();
        }

        [Fact]
        public async Task Should_throw_if_multiple_metadata_get_returned_by_query()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            var latestArtifact = Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts");

            _artifactApi.SetupExecuteQuery(query, [
                latestArtifact,
                Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.0.json", "enginehosts", DateTimeOffset.UtcNow - TimeSpan.FromDays(5)),
                ]);

            // Act
            var act = async () => await client.QueryZipArtifactItem(latestArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public class QueryZipArtifactItem : EngineHostArtifactApiTests
    {
        [Fact]
        public async Task Should_zip_artifact_for_metadata_artifact()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts");
            var zipArtifactName = "0.28.0-linux-x64.zip";
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"0.28.0-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, Substitute.For<IArtifactItem>().Setup(zipArtifactName, "enginehosts"));

            var metadata = new ArtifactMetadata { Name = "Test", Version = "0.28.0", MinCoreVersion = "0.48.0" };
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
            var client = SetupEngineHostLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("0.28.x-bsi-x64_0.48.1.json", "enginehosts");

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_downloaded()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts");

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_throw_if_metadata_cannot_be_deserialized()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var metadataArtifact = Substitute.For<IArtifactItem>().Setup("0.28.0-linux-x64_0.48.1.json", "enginehosts");
            await _artifactApi.SetupMetadataDownload(metadataArtifact, new ClusterImportResultItem());   // !no metadata!

            // Act
            var act = async () => await client.QueryZipArtifactItem(metadataArtifact, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }

        [Fact]
        public async Task Should_return_null_if_no_artifact_found_for_core_version()
        {
            // Arrange
            var client = SetupEngineHostLoader();
            var coreVersion = new SemVersion(0, 48, 0);
            var query = "items.find({\"repo\":\"vicione-suite\",\"$and\":[{\"path\":{\"$match\":\"enginehost\"}},{\"name\":{\"$match\":\"*-win-x64_0.48.*.json\"}}]}).sort({\"$desc\":[\"updated\"]})";
            _artifactApi.SetupExecuteQuery(query, []);

            // Act
            var result = await client.QueryLatestZipArtifactItem(coreVersion, CancellationToken.None);

            // Assert            
            result.Should().BeNull();
        }
    }
}
