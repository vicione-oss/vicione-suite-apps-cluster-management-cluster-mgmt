using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Engines.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Backend.IO;
using Sdk.Backend.Messaging;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.Instance.Events;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Snapshots;

/// <summary>
/// Issue #380: parallel changes of one cluster, run against the real snapshot store.
/// The <see cref="ControllableAtomicFileWriter"/> holds the first change between its info and its snapshot write,
/// which is where a second change of the same cluster used to slip in.
/// </summary>
public class ParallelClusterChangesTests
{
    // How long the second change gets to run into the first one before the first one goes on.
    // A serialized second change waits for the first, so it only slows the passing test down.
    private static readonly TimeSpan s_secondChangeHeadStart = TimeSpan.FromMilliseconds(200);

    private readonly MockFileSystem _fileSystem = new();
    private readonly ControllableAtomicFileWriter _writer;
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    protected ParallelClusterChangesTests()
    {
        _writer = new(_fileSystem);
        _workspaceProvider.Home.Returns("home");
    }

    private IServiceCollection AddSnapshotServices(IServiceCollection services)
        => services
            .AddLogging()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton<IAtomicFileWriter>(_writer)
            .AddSingleton(_workspaceProvider)
            .AddSingleton(TestDataFactory.SetupMqttDataPortDesignProvider())
            .AddSingleton(Substitute.For<ISuiteRulesetProvider>())
            .AddSingleton(Substitute.For<ISuiteMediator>())
            .AddSnapshotStore();

    private ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var services = AddSnapshotServices(new ServiceCollection())
            .AddSingleton<IClusterDbContext>(_ =>
            {
                var connection = new SqliteConnection($"DataSource={TestDbContextFactory.DataSourceInMemory}");
                connection.Open();

                var dbContext = TestDbContextFactory.CreateSqliteContext<ClusterDbContextSqlite>(connection);
                dbContext.Database.EnsureCreated();
                return dbContext;
            });

        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static IClusterSnapshotStore Store(IServiceProvider services)
        => services.GetRequiredService<IClusterSnapshotStore>();

    private static async Task<Version[]> StoredVersions(IServiceProvider services, Guid clusterId)
        => [.. (await Store(services).GetClusterInfos(clusterId, CancellationToken.None)).Select(i => i.Version).Order()];

    private static Task Consume<TConsumer, TMessage>(IServiceProvider services, TMessage message)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        var context = Substitute.For<ConsumeContext<TMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);

        return Task.Run(async () =>
        {
            using var scope = services.CreateScope();
            await ActivatorUtilities.CreateInstance<TConsumer>(scope.ServiceProvider).Consume(context);
        });
    }

    private static async Task<CommitClusterChange> CreateCommit(Cluster cluster, CommitClusterOptions? options)
        => new(await ClusterSerializer.CompressAsync(cluster, cancellationToken: CancellationToken.None), new CommitClusterMetaInfo(cluster), options);

    /// <summary>
    /// Runs <paramref name="first"/> until it is held before its snapshot write, then <paramref name="second"/>,
    /// and lets the first one go on once the second one finished or had its head start.
    /// </summary>
    private async Task<(Exception? First, Exception? Second)> RunSecondWhileFirstIsStoring(Func<Task> first, Func<Task> second)
    {
        _writer.HoldNext(ControllableAtomicFileWriter.IsSnapshot);
        var firstTask = first();
        await _writer.Held.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var secondTask = second();
        await Task.WhenAny(secondTask, Task.Delay(s_secondChangeHeadStart, TestContext.Current.CancellationToken));
        _writer.Release();

        return (await Catch(firstTask), await Catch(secondTask));

        static async Task<Exception?> Catch(Task task)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }

    public class Stores : ParallelClusterChangesTests
    {
        [Fact]
        public async Task Failed_store_of_a_version_being_stored_should_keep_that_version()
        {
            // Arrange
            await using var services = CreateServices();
            var cluster = ClusterDbContextTools.CreateCluster(version: new Version(1, 0, 2));

            // Act
            var (first, second) = await RunSecondWhileFirstIsStoring(
                () => Store(services).Store(cluster, CancellationToken.None),
                () => Store(services).Store(cluster, CancellationToken.None));

            // Assert
            first.Should().BeNull();
            second.Should().BeOfType<InvalidOperationException>("the version is stored by the first store");
            (await StoredVersions(services, cluster.Id)).Should().Equal([cluster.Version],
                "the cleanup of the failed store must not delete the files of the store that succeeded");
            (await Store(services).Load(cluster.Id, cluster.Version, CancellationToken.None)).Should().NotBeNull();
        }
    }

    public class Commits : ParallelClusterChangesTests
    {
        [Fact]
        public async Task Parallel_incremented_commits_should_store_consecutive_versions()
        {
            // Arrange
            await using var services = CreateServices();
            var cluster = ClusterDbContextTools.CreateCluster(version: new Version(1, 0, 1));
            await Store(services).Store(cluster, CancellationToken.None);
            var commit = await CreateCommit(cluster, new CommitClusterOptions { IncrementVersion = true });

            // Act
            var (first, second) = await RunSecondWhileFirstIsStoring(
                () => Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, commit),
                () => Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, commit));

            // Assert
            first.Should().BeNull();
            second.Should().BeNull();
            (await StoredVersions(services, cluster.Id)).Should().Equal(new Version(1, 0, 1), new Version(1, 0, 2), new Version(1, 0, 3));
        }
    }

    public sealed class EngineToggles : ParallelClusterChangesTests, IDisposable
    {
        private readonly Guid _engine1 = Guid.NewGuid();
        private readonly Guid _engine2 = Guid.NewGuid();
        private readonly ClusterBuilder _builder = ClusterFactory.CreateComplexCluster();

        private async Task<Cluster> SetupActiveClusterWithStoppedEngines(IServiceProvider services)
        {
            _builder.Editors.Engine.SetEnabled(_builder.CreateTopologyToEngine("Engine1", _engine1), false);
            _builder.Editors.Engine.SetEnabled(_builder.CreateTopologyToEngine("Engine2", _engine2), false);

            services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(_builder.Cluster);
            await Store(services).Store(_builder.Cluster, CancellationToken.None);
            return _builder.Cluster;
        }

        private static async Task<Cluster?> LoadSnapshot(IServiceProvider services, Cluster cluster)
            => await Store(services).Load(cluster.Id, cluster.Version, CancellationToken.None);

        private static bool IsEnabled(Cluster snapshot, Guid engineId)
            => snapshot.NodeGroups.SelectMany(g => g.Nodes).SelectMany(n => n.Applications)
                .SelectMany(a => a.EngineHosts).SelectMany(h => h.Engines)
                .Single(e => e.Id == engineId).Enabled;

        [Fact]
        public async Task Failed_snapshot_write_should_keep_the_active_snapshot()
        {
            // Arrange
            await using var services = CreateServices();
            var cluster = await SetupActiveClusterWithStoppedEngines(services);
            _writer.FailNext(ControllableAtomicFileWriter.IsSnapshot);

            // Act
            var act = () => Consume<ToggleEngineConsumer, ToggleEngine>(services, new(_engine1, true));

            // Assert
            await act.Should().ThrowAsync<IOException>();
            (await StoredVersions(services, cluster.Id)).Should().Equal([cluster.Version]);
            var snapshot = await LoadSnapshot(services, cluster);
            snapshot.Should().NotBeNull("the active cluster must never be left without its snapshot");
            IsEnabled(snapshot, _engine1).Should().BeFalse();
        }

        [Fact]
        public async Task Parallel_toggles_should_keep_both_changes()
        {
            // Arrange
            await using var services = CreateServices();
            var cluster = await SetupActiveClusterWithStoppedEngines(services);

            // Act
            var (first, second) = await RunSecondWhileFirstIsStoring(
                () => Consume<ToggleEngineConsumer, ToggleEngine>(services, new(_engine1, true)),
                () => Consume<ToggleEngineConsumer, ToggleEngine>(services, new(_engine2, true)));

            // Assert
            first.Should().BeNull();
            second.Should().BeNull();
            var snapshot = await LoadSnapshot(services, cluster);
            snapshot.Should().NotBeNull();
            IsEnabled(snapshot, _engine1).Should().BeTrue();
            IsEnabled(snapshot, _engine2).Should().BeTrue();
        }

        [Fact]
        public async Task Settings_update_during_a_toggle_should_keep_the_toggle()
        {
            // Arrange: the settings update builds the complex cluster with its designs
            var designProvider = Substitute.For<IDesignProvider>();
            designProvider.CreateResolver().Returns(_builder.DependencyResolver);
            await using var services = CreateServices(s => s.AddSingleton(designProvider));
            var cluster = await SetupActiveClusterWithStoppedEngines(services);

            // Act
            var (first, second) = await RunSecondWhileFirstIsStoring(
                () => Consume<ToggleEngineConsumer, ToggleEngine>(services, new(_engine1, true)),
                () => Consume<UpdateFunctionBlockSettingsConsumer, UpdateFunctionBlockSettings>(services, new(cluster.Id, [])));

            // Assert
            first.Should().BeNull();
            second.Should().BeNull();
            var snapshot = await LoadSnapshot(services, cluster);
            snapshot.Should().NotBeNull();
            IsEnabled(snapshot, _engine1).Should().BeTrue("the settings update starts from the toggled snapshot");
        }

        public void Dispose() => _builder.Dispose();
    }

    public class FunctionBlockSettingsUpdates : ParallelClusterChangesTests
    {
        [Fact]
        public async Task Failed_snapshot_write_should_keep_the_active_snapshot()
        {
            // Arrange
            await using var services = CreateServices();
            // Without function blocks: the settings are not the point, the snapshot write is
            var cluster = ClusterDbContextTools.CreateCluster();
            services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(cluster);
            await Store(services).Store(cluster, CancellationToken.None);
            _writer.FailNext(ControllableAtomicFileWriter.IsSnapshot);

            // Act
            var act = () => Consume<UpdateFunctionBlockSettingsConsumer, UpdateFunctionBlockSettings>(services,
                new(cluster.Id, []));

            // Assert
            await act.Should().ThrowAsync<IOException>();
            (await StoredVersions(services, cluster.Id)).Should().Equal([cluster.Version]);
            (await Store(services).Load(cluster.Id, cluster.Version, CancellationToken.None))
                .Should().NotBeNull("the active cluster must never be left without its snapshot");
        }
    }

    public class Removes : ParallelClusterChangesTests
    {
        [Fact]
        public async Task Remove_of_a_version_being_stored_should_wait_and_remove_it()
        {
            // Arrange
            await using var services = CreateServices();
            var active = ClusterDbContextTools.CreateCluster(version: new Version(1, 0, 1));
            services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(active);
            await Store(services).Store(active, CancellationToken.None);
            var commit = await CreateCommit(ClusterDbContextTools.CreateCluster(active.Id, new Version(1, 0, 2)), null);

            // Act
            var (first, second) = await RunSecondWhileFirstIsStoring(
                () => Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, commit),
                () => Consume<RemoveClusterConsumer, RemoveCluster>(services, new(active.Id, new Version(1, 0, 2))));

            // Assert
            first.Should().BeNull();
            second.Should().BeNull("the remove runs once the version is stored");
            (await StoredVersions(services, active.Id)).Should().Equal([active.Version]);
            _fileSystem.AllFiles.Should().NotContain(f => f.Contains("1.0.2", StringComparison.Ordinal), "nothing of the removed version is left");
        }
    }

    public class DeploymentCommit : ParallelClusterChangesTests
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly Cluster _active = ClusterDbContextTools.CreateCluster(version: new Version(1, 0, 1));
        private readonly IInstanceInformationProvider _instances = Substitute.For<IInstanceInformationProvider>();

        // The saga runs on the real store; the commits are consumed by the test
        private ServiceProvider CreateDeploymentServices()
        {
            // The saga's store and the consumers' share one lock, as in the module
            var clusterLock = new ClusterLock();
            var store = new ClusterSnapshotStore(_workspaceProvider, _fileSystem, _writer, clusterLock, NullLogger<ClusterSnapshotStore>.Instance);
            var collection = new ServiceCollection().ConfigureDeployClusterSaga(store, x => x.AddSagaStateMachine<DeployClusterStateMachine, DeployClusterState>());
            AddSnapshotServices(collection)
                .AddSingleton<IClusterSnapshotStore>(store)
                .AddSingleton<IClusterLock>(clusterLock)
                .AddSingleton(_instances);
            return collection.BuildServiceProvider(true);
        }

        /// <summary>
        /// Starts a deployment that commits a new version and returns its commit, which no consumer has received yet.
        /// </summary>
        private async Task<(ITestHarness Harness, CommitClusterChange DeploymentCommit)> StartDeploymentCommit(IServiceProvider services)
        {
            services.GetRequiredService<IClusterDbContext>().SaveActiveClusterVersion(_active);
            await Store(services).Store(_active, CancellationToken.None);

            var harness = services.GetTestHarness();
            await harness.Start();
            var deployOptions = new DeployClusterOptions(await ClusterSerializer.CompressAsync(_active, cancellationToken: CancellationToken.None));
            await harness.DeployCluster(_correlationId, _active.Id, null, deployOptions);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);
            // The first sent commit, without waiting for the harness to go idle: it stops the bus once it is
            await foreach (var sent in harness.Sent.SelectAsync<CommitClusterChange>(TestContext.Current.CancellationToken))
                return (harness, sent.Context.Message);

            throw new InvalidOperationException("The deployment sent no commit");
        }

        private Task<CommitClusterChange> CreateEditorCommit()
            => CreateCommit(_active, new CommitClusterOptions { IncrementVersion = true });

        [Fact]
        public async Task Editor_commit_during_a_deployment_commit_should_not_take_the_deployment_version()
        {
            // Arrange
            await using var services = CreateDeploymentServices();
            var (_, deploymentCommit) = await StartDeploymentCommit(services);

            // Act: an editor save is consumed before the deployment's commit
            await Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, await CreateEditorCommit());
            var act = () => Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, deploymentCommit);

            // Assert
            await act.Should().NotThrowAsync("the deployment's commit version must not be taken by another commit");
            var deploymentVersion = deploymentCommit.MetaInfo.ClusterVersion;
            deploymentVersion.Should().Be(new Version(1, 0, 2));
            (await StoredVersions(services, _active.Id)).Should().Equal(new Version(1, 0, 1), deploymentVersion, new Version(1, 0, 3));
            services.GetRequiredService<IClusterLock>().GetReservedVersions(_active.Id).Should().BeEmpty("the stored version is no longer reserved");
        }

        [Fact]
        public async Task Instance_joining_during_a_deployment_commit_should_not_take_the_deployment_version()
        {
            // Arrange: the instance joins the cluster's only node group
            _active.NodeGroups.Add(new ClusterNodeGroup { Id = Guid.NewGuid() });
            var instance = Substitute.For<IInstanceInformation>();
            instance.Id.Returns(Guid.NewGuid());
            instance.Type.Returns(InstanceType.Slave);
            instance.InstalledModules.Returns([]);
            _instances.GetInstancesInCluster(Arg.Any<CancellationToken>()).Returns([instance]);

            await using var services = CreateDeploymentServices();
            var (_, deploymentCommit) = await StartDeploymentCommit(services);

            // Act: the instance's commit is consumed before the deployment's commit
            await Consume<InstanceCreatedConsumer, InstanceCreated>(services, new(instance.Id));
            var instanceCommit = services.GetRequiredService<ISuiteMediator>().ReceivedCalls()
                .SelectMany(c => c.GetArguments()).OfType<CommitClusterChange>().Single();
            await Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, instanceCommit);
            var act = () => Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, deploymentCommit);

            // Assert
            await act.Should().NotThrowAsync("the deployment's commit version must not be taken by the instance's commit");
            var deploymentVersion = deploymentCommit.MetaInfo.ClusterVersion;
            (await StoredVersions(services, _active.Id)).Should().Equal(new Version(1, 0, 1), deploymentVersion, new Version(1, 0, 3));
            var joined = await Store(services).Load(_active.Id, new Version(1, 0, 3), CancellationToken.None);
            joined!.NodeGroups[0].Nodes.Should().ContainSingle(n => n.Applications.Any(a => a.Id == instance.Id));
            services.GetRequiredService<IClusterLock>().GetReservedVersions(_active.Id).Should().BeEmpty();
        }

        [Fact]
        public async Task Failed_deployment_commit_should_free_the_deployment_version()
        {
            // Arrange
            await using var services = CreateDeploymentServices();
            var (harness, deploymentCommit) = await StartDeploymentCommit(services);

            // Act
            await harness.PublishClusterChangeFailed(_correlationId, _active.Id, deploymentCommit.MetaInfo.ClusterVersion);
            await harness.AssertClusterUpdateFailed(_correlationId, deploymentCommit.MetaInfo.ClusterVersion);
            await Consume<CommitClusterChangeConsumer, CommitClusterChange>(services, await CreateEditorCommit());

            // Assert
            (await StoredVersions(services, _active.Id)).Should().Equal(new Version(1, 0, 1), deploymentCommit.MetaInfo.ClusterVersion);
        }
    }
}
