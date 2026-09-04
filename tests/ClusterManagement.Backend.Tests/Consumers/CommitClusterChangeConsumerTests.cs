using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Consumers;

public class CommitClusterChangeConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _storage = Substitute.For<IClusterSnapshotStore>();
    private readonly ISuiteRulesetProvider _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();

    public CommitClusterChangeConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<CommitClusterChangeConsumer>();
            cfg.AddSingleton(_ => _storage);
            cfg.AddSingleton(_ => _rulesetProvider);
            cfg.AddTransient(_ => TestDataFactory.SetupMqttDataPortDesignProvider());
        };

    public class CommitWithoutOptions : CommitClusterChangeConsumerTests
    {
        [Fact]
        public async Task Command_should_be_consumed()
        {
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);

            // Arrange
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), null);

            // Act + Assert
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);
        }

        [Fact]
        public async Task Insert_valid_cluster_should_publish_changed_event()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();

            cluster.Version = new Version(1, 0, 1);
            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), null);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id, TestContext.Current.CancellationToken));

            await _storage.Received(1).Store(Arg.Any<Cluster>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Insert_cluster_without_options_should_store_cluster_directly()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var options = new CommitClusterOptions();

            cluster.Version = new Version(1, 0, 1);
            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id, TestContext.Current.CancellationToken));

            await _storage.Received(1).Store(Arg.Any<Cluster>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_publish_commit_failed_event_on_failure()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(
                compressed,
                new CommitClusterMetaInfo(cluster),
                null);

            _storage.Store(Arg.Any<Cluster>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("Storage failed"));

            // Act
            var act = async () => await tester.TestCommandFault<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            Assert.True(await tester.Harness.Published.Any<CommitClusterChangeFailed>(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_sync_metaversion_to_cluster_when_higher()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();

            // Cluster has version 1.0.1
            cluster.Version = new Version(1, 0, 1);

            // MetaInfo has higher version 1.0.5
            var metaInfo = new CommitClusterMetaInfo(cluster)
            {
                ClusterVersion = new Version(1, 0, 5)
            };

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(
                compressed,
                metaInfo,
                null); // Options is null to trigger version sync logic

            // Act
            var changeEvent = await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer, ClusterChanged>(command);

            // Assert
            await _storage.Received(1).Store(
                Arg.Is<Cluster>(c =>
                    c.Version == new Version(1, 0, 5) &&
                    c.VersionCreated > DateTime.UtcNow.AddSeconds(-5)),
                Arg.Any<CancellationToken>());

            changeEvent.Version.Should().Be(new Version(1, 0, 5));
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_not_sync_metaversion_when_cluster_version_is_higher()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();

            // Cluster has version 2.0.0
            cluster.Version = new Version(2, 0, 0);
            var originalVersionCreated = cluster.VersionCreated;

            // MetaInfo has lower version 1.5.0
            var metaInfo = new CommitClusterMetaInfo(cluster)
            {
                ClusterVersion = new Version(1, 5, 0)
            };

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(
                compressed,
                metaInfo,
                null);

            // Act
            var changeEvent = await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer, ClusterChanged>(command);

            // Assert
            await _storage.Received(1).Store(
                Arg.Is<Cluster>(c =>
                    c.Version == new Version(2, 0, 0)), // Version should remain unchanged
                Arg.Any<CancellationToken>());

            changeEvent.Version.Should().Be(new Version(1, 5, 0)); // Event uses metaVersion
        }
    }

    public class CommitWithOptions : CommitClusterChangeConsumerTests
    {
        [Fact]
        public async Task Increment_version_should_modify_cluster_and_publish_change_event()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var options = new CommitClusterOptions
            {
                IncrementVersion = true,
            };

            cluster.Version = new Version(1, 0, 1);
            var expectedVersion = new Version(1, 0, 2);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            var changeEvent = await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer, ClusterChanged>(command);

            // Assert
            await _storage.Received(1).Store(Arg.Is<Cluster>(k => k.Version == expectedVersion), Arg.Any<CancellationToken>());
            changeEvent.Version.Should().Be(expectedVersion);
        }

        [Fact]
        public async Task Should_not_increment_version_if_disabled_by_options()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var options = new CommitClusterOptions
            {
                IncrementVersion = false,
            };

            cluster.Version = new Version(1, 0, 1);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            var changeEvent = await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer, ClusterChanged>(command);

            // Assert
            await _storage.Received(1).Store(Arg.Is<Cluster>(k => k.Version == cluster.Version), Arg.Any<CancellationToken>());
            changeEvent.Version.Should().Be(cluster.Version);
        }

        [Fact]
        public async Task Cleanup_dependencies_should_modify_cluster_and_publish_change_event()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var options = new CommitClusterOptions
            {
                CleanupDependencies = true,
            };

            cluster.Version = new Version(1, 0, 1);

            var vseDesignId1 = Guid.NewGuid();
            var vseDesignId2 = Guid.NewGuid();
            var vseDesignDependency = new ClusterDependency
            {
                Name = "VSE.IoT.Core",
                Version = "0.8.1",
            };

            cluster.FunctionBlockDesignIds.Add(vseDesignId1);
            cluster.FunctionBlockDesignIds.Add(vseDesignId2);
            cluster.Dependencies.Add(vseDesignDependency);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            var command = new CommitClusterChange(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id, TestContext.Current.CancellationToken));

            await _storage.Received(1).Store(Arg.Is<Cluster>(k => k.Dependencies.Count == 1), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_cleanup_dependencies_if_disabled_by_options()
        {
            // Arrange
            await using MassTransitTester tester = new(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            CommitClusterOptions options = new()
            {
                CleanupDependencies = false,
            };

            cluster.Version = new(1, 0, 1);

            var vseDesignId1 = Guid.NewGuid();
            var vseDesignId2 = Guid.NewGuid();
            ClusterDependency vseDesignDependency = new()
            {
                Name = "VSE.IoT.Core",
                Version = "0.8.1",
            };

            cluster.FunctionBlockDesignIds.Add(vseDesignId1);
            cluster.FunctionBlockDesignIds.Add(vseDesignId2);
            cluster.Dependencies.Add(vseDesignDependency);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            CommitClusterChange command = new(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id, TestContext.Current.CancellationToken));

            await _storage.Received(1).Store(Arg.Is<Cluster>(k => k.Dependencies.Count == 2), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Ensures_DefaultPoolings_in_dependencies(bool cleanupDependencies)
        {
            // Arrange
            await using MassTransitTester tester = new(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            CommitClusterOptions options = new()
            {
                CleanupDependencies = cleanupDependencies,
            };

            cluster.Version = new(1, 0, 1);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);

            var compressed = await ClusterSerializer.CompressAsync(cluster, cancellationToken: TestContext.Current.CancellationToken);
            CommitClusterChange command = new(compressed, new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>(TestContext.Current.CancellationToken));
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id, TestContext.Current.CancellationToken));

            await _storage.Received(1)
                .Store(FluentArg.Assert<Cluster>(k => k.Dependencies.Should().ContainSingle().Which.Should().BeEquivalentTo(new ClusterDependency()
                {
                    Name = "ViciOne.Engine.DefaultPoolings",
                    Version = "1.0.0",
                }))!, Arg.Any<CancellationToken>());
        }
    }
}
