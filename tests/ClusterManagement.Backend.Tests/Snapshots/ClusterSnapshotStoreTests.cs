using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools.Factories;
using ClusterManagement.Backend.Snapshots.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.IO;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Snapshots;

public class ClusterSnapshotStoreTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MockFileSystem _fileSystem = new();
    private readonly IOptions<ClusterManagementOptions> _options = Substitute.For<IOptions<ClusterManagementOptions>>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _wsProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    protected ClusterSnapshotStoreTests()
    {
        _wsProvider.Home.Returns("home");

        _serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_ => _wsProvider)
            .AddSingleton<IFileSystem>(_ => _fileSystem)
            .AddSingleton(_ => _options)
            .AddSingleton<IAtomicFileWriter, TestAtomicFileWriter>()
            .AddTransient<IClusterSnapshotStore, ClusterSnapshotStore>()
            .BuildServiceProvider();
    }

    private string GetClusterFilePath(Guid id, Version version)
        => _fileSystem.Path.Combine(_fileSystem.GetSnapshotsPath(_wsProvider),
            ClusterSnapshotStore.GetStorageFileName(id, version));

    private string GetClusterInfoPath(Guid id, Version version)
        => _fileSystem.Path.Combine(_fileSystem.GetSnapshotsPath(_wsProvider),
            ClusterSnapshotStore.GetStorageInfoName(id, version));

    public class StoreCluster : ClusterSnapshotStoreTests
    {
        [Fact]
        public async Task StoreShouldCreateClusterGzipFile()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();

            // Act
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Assert
            _fileSystem.FileExists(GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version)).Should().BeTrue();
        }

        [Fact]
        public async Task StoreBigClusterShouldCreateClusterGzipFile()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();

            // todo
            //VseFactory.AddVseContainers(builder, Guid.NewGuid(), 5, "102.234.234.234");

            // Act
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Assert
            _fileSystem.FileExists(GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version)).Should().BeTrue();
        }

        [Fact]
        public async Task StoreShouldCreateClusterInfoFile()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();

            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Assert
            _fileSystem.FileExists(GetClusterInfoPath(builder.Cluster.Id, builder.Cluster.Version)).Should().BeTrue();
        }

        // Issue #368: a failed store must never remove the version it found
        [Fact]
        public async Task StoreOfAnExistingVersionShouldFailAndKeepIt()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Act
            var act = () => snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task StoreAfterALeftoverInfoShouldSucceedOnRetry()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);
            // a crash between the info and the snapshot write leaves only the info
            _fileSystem.File.Delete(GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version));
            var failingStore = () => snapshotStore.Store(builder.Cluster, CancellationToken.None);
            await failingStore.Should().ThrowAsync<InvalidOperationException>();

            // Act
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Assert
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task StoreOfAnExistingCompressedVersionShouldFailAndKeepIt()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);
            var compressed = await ClusterSerializer.CompressAsync(builder.Cluster, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var act = () => snapshotStore.Store(compressed, new ClusterSnapshotInfo(builder.Cluster.Id, builder.Cluster.Version, builder.Cluster.Name), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task CompressedStoreAfterALeftoverInfoShouldSucceedOnRetry()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);
            // a crash between the info and the snapshot write leaves only the info
            _fileSystem.File.Delete(GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version));
            var compressed = await ClusterSerializer.CompressAsync(builder.Cluster, cancellationToken: TestContext.Current.CancellationToken);
            var info = new ClusterSnapshotInfo(builder.Cluster.Id, builder.Cluster.Version, builder.Cluster.Name);
            var failingStore = () => snapshotStore.Store(compressed, info, CancellationToken.None);
            await failingStore.Should().ThrowAsync<InvalidOperationException>();

            // Act
            await snapshotStore.Store(compressed, info, CancellationToken.None);

            // Assert
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task ReplacingStoreThatFailsShouldKeepTheExistingVersion()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Act
            var act = () => snapshotStore.Store(builder.Cluster.Id, builder.Cluster.Version, InfoOf(builder.Cluster), builder.Cluster,
                (_, _, _) => throw new InvalidOperationException("Write failed"), true, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Write failed");
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task StoreWithoutReplaceShouldCreateANewVersion()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();

            // Act
            await snapshotStore.Store(builder.Cluster.Id, builder.Cluster.Version, InfoOf(builder.Cluster), builder.Cluster, WriteCluster, false, CancellationToken.None);

            // Assert
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task StoreWithoutReplaceShouldFailAndKeepAnExistingVersion()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);

            // Act
            var act = () => snapshotStore.Store(builder.Cluster.Id, builder.Cluster.Version, InfoOf(builder.Cluster), builder.Cluster, WriteCluster, false, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            await AssertStored(snapshotStore, builder.Cluster);
        }

        [Fact]
        public async Task StoreWithoutReplaceAfterALeftoverInfoShouldSucceed()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            await snapshotStore.Store(builder.Cluster, CancellationToken.None);
            // a crash between the info and the snapshot write leaves only the info
            _fileSystem.File.Delete(GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version));

            // Act
            await snapshotStore.Store(builder.Cluster.Id, builder.Cluster.Version, InfoOf(builder.Cluster), builder.Cluster, WriteCluster, false, CancellationToken.None);

            // Assert
            await AssertStored(snapshotStore, builder.Cluster);
        }

        // The info written by the snapshot migration, the only caller of the generic store
        private static JsonObject InfoOf(Cluster cluster) => new()
        {
            { nameof(Cluster.Id), cluster.Id },
            { nameof(Cluster.Version), cluster.Version.ToString() },
            { nameof(Cluster.Name), cluster.Name },
        };

        private static Task WriteCluster(Stream stream, Cluster cluster, CancellationToken cancellationToken)
            => JsonSerializer.SerializeAsync(stream, cluster, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);

        // Listed and loadable, as the Cluster Manager and a deployment need it
        private static async Task AssertStored(IClusterSnapshotStore snapshotStore, Cluster cluster)
        {
            (await snapshotStore.GetClusterInfos(cluster.Id, CancellationToken.None))
                .Should().ContainSingle(i => i.Version == cluster.Version && i.Name == cluster.Name);

            var loaded = await snapshotStore.Load(cluster.Id, cluster.Version, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded.Name.Should().Be(cluster.Name);
        }
    }
    public class LoadCluster : ClusterSnapshotStoreTests
    {
        [Fact]
        public async Task LoadShouldReturnCluster()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            var clusterFilePath = GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version);
            await using var fileStream = _fileSystem.FileStream.New(clusterFilePath, FileMode.Create);
            var clusterCompressed = await ClusterSerializer.CompressAsync(builder.Cluster, cancellationToken: TestContext.Current.CancellationToken);
            await fileStream.WriteAsync(clusterCompressed, TestContext.Current.CancellationToken);
            fileStream.Close();

            // Act
            var cluster = await snapshotStore.Load(builder.Cluster.Id, builder.Cluster.Version, CancellationToken.None);

            // Assert
            cluster.Should().NotBeNull();
            cluster!.Id.Should().Be(builder.Cluster.Id);
            cluster.Version.Should().Be(builder.Cluster.Version);
        }

        [Fact]
        public async Task LoadJsonShouldReturnJson()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            var clusterFilePath = GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version);
            var clusterCompressed = await ClusterSerializer.CompressAsync(builder.Cluster, cancellationToken: TestContext.Current.CancellationToken);

            await using var fileStream = _fileSystem.FileStream.New(clusterFilePath, FileMode.Create);
            await fileStream.WriteAsync(clusterCompressed, TestContext.Current.CancellationToken);
            fileStream.Close();

            // Act
            var cluster = await snapshotStore.LoadCompressedJson(builder.Cluster.Id, builder.Cluster.Version, CancellationToken.None);

            // Assert
            cluster.Should().BeEquivalentTo(clusterCompressed);
        }
    }

    public class DeleteCluster : ClusterSnapshotStoreTests
    {
        [Fact]
        public void DeleteExistingShouldRemoveFiles()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            using var builder = ClusterFactory.CreateComplexCluster();
            var clusterFilePath = GetClusterFilePath(builder.Cluster.Id, builder.Cluster.Version);
            var clusterInfoPath = GetClusterInfoPath(builder.Cluster.Id, builder.Cluster.Version);
            _fileSystem.AddFile(clusterFilePath, "[]");
            _fileSystem.AddFile(clusterInfoPath, "[]");

            // Act
            snapshotStore.Delete(builder.Cluster.Id, builder.Cluster.Version);

            // Assert
            _fileSystem.FileExists(clusterFilePath).Should().BeFalse();
            _fileSystem.FileExists(clusterInfoPath).Should().BeFalse();
        }
    }

    public class GetClusterInfos : ClusterSnapshotStoreTests
    {
        private static readonly Guid[] s_testClusterIds =
        [
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
        ];

        private static readonly IEnumerable<ClusterInfo> s_testData =
        [
            new(s_testClusterIds[0], new Version(1, 0, 0), "Name1-1"),
            new(s_testClusterIds[0], new Version(2, 0, 0), "Name1-2"),
            new(s_testClusterIds[0], new Version(2, 1, 0), "Name1-3"),
            new(s_testClusterIds[0], new Version(2, 2, 0), "Name1-4"),
            new(s_testClusterIds[0], new Version(2, 2, 1), "Name1-5"),

            new(s_testClusterIds[1], new Version(1, 0, 1), "Name2-1"),
            new(s_testClusterIds[1], new Version(1, 3, 2), "Name2-2"),
            new(s_testClusterIds[1], new Version(1, 3, 6), "Name2-3"),
            new(s_testClusterIds[1], new Version(1, 4, 0), "Name2-4"),

            new(s_testClusterIds[2], new Version(1, 0), "Name3-1"),
            new(s_testClusterIds[2], new Version(2, 0), "Name3-2"),
            new(s_testClusterIds[2], new Version(3, 0), "Name3-3"),
            new(s_testClusterIds[2], new Version(4, 0), "Name3-4"),
        ];

        private static async Task InsertTestData(IClusterSnapshotStore snapshotStore)
        {
            foreach (var info in s_testData)
            {
                var cluster = new Cluster
                {
                    Id = info.Id,
                    Version = info.Version,
                    Name = info.Name
                };
                await snapshotStore.Store(cluster, CancellationToken.None);
            }
        }

        [Fact]
        public async Task GetClusterInfosShouldReturnAvailableInfos()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            await InsertTestData(snapshotStore);

            // Act
            var result = await snapshotStore.GetClusterInfos(null, CancellationToken.None);

            //Assert
            result.Should().BeEquivalentTo(s_testData);
        }

        [Fact]
        public async Task ExistsShouldBeTrueIfFileExists()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            await InsertTestData(snapshotStore);

            //Act
            var result = snapshotStore.Exists(s_testData.First().Id, s_testData.First().Version);

            //Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsShouldBeFalseForMissingVersion()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            await InsertTestData(snapshotStore);

            //Act
            var result = snapshotStore.Exists(s_testData.First().Id, new Version(47, 0));

            //Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsShouldBeFalseForMissingClusterId()
        {
            // Arrange
            var snapshotStore = _serviceProvider.GetRequiredService<IClusterSnapshotStore>();
            await InsertTestData(snapshotStore);

            //Act
            var result = snapshotStore.Exists(Guid.NewGuid(), new Version(1, 0));

            //Assert
            result.Should().BeFalse();
        }
    }
}
