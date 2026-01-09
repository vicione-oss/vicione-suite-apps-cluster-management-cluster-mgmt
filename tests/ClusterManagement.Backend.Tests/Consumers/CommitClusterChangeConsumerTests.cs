using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using Xunit;
using ClusterManagement.Backend.Tests.TestUtils;

namespace ClusterManagement.Backend.Tests.Consumers;

public class CommitClusterChangeConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _storage = Substitute.For<IClusterSnapshotStore>();
    private readonly IDependencyResolver _resolver = Substitute.For<IDependencyResolver>();
    private readonly ISuiteRulesetProvider _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();

    public CommitClusterChangeConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<CommitClusterChangeConsumer>();
            cfg.AddSingleton(_ => _storage);
            cfg.AddSingleton(_ => _resolver);
            cfg.AddSingleton(_ => _rulesetProvider);
            cfg.AddTransient(_ => TestDataFactory.SetupMqttDataPortDesignProvider());
            cfg.AddTransient(_ => Substitute.For<IInstanceInformationProvider>());
            cfg.AddTransient(_ => Substitute.For<IMqttBrokerConnectionCache>());
        };
    }

    public class CommitWithoutOptions : CommitClusterChangeConsumerTests
    {
        [Fact]
        public async Task Command_should_be_consumed()
        {
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();

            // Arrange
            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), null);

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
            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), null);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>());
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id));

            await _storage.Received(1).Store(Arg.Any<byte[]>(), Arg.Any<ClusterSnapshotInfo>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Insert_cluster_without_options_should_store_cluster_directly()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var cluster = ClusterDbContextTools.CreateCluster();
            var options = new CommitClusterOptions();

            cluster.Version = new Version(1, 0, 1);
            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>());
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id));

            await _storage.Received(1).Store(Arg.Any<Cluster>(), Arg.Any<CancellationToken>());
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


            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), options);

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


            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), options);

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
                Version = new Version(0, 8, 1),
            };

            cluster.FunctionBlockDesignIds.Add(vseDesignId1);
            cluster.FunctionBlockDesignIds.Add(vseDesignId2);

            _storage.GetClusterInfos(cluster.Id, Arg.Any<CancellationToken>())
                .Returns(
                [
                    new(cluster.Id, cluster.Version, cluster.Name, true),
                ]);


            var command = new CommitClusterChange(ClusterSerializer.Compress(cluster), new CommitClusterMetaInfo(cluster), options);

            // Act
            await tester.TestCommand<CommitClusterChange, CommitClusterChangeConsumer>(command);

            // Assert
            Assert.False(await tester.Harness.Published.Any<ClusterError>());
            Assert.True(await tester.Harness.Published.Any<ClusterChanged>(r =>
                r.Context.Message.ClusterId == cluster.Id));

            await _storage.Received(1).Store(Arg.Is<Cluster>(k => k.Dependencies.Count <= 0), Arg.Any<CancellationToken>());
        }
    }
}
