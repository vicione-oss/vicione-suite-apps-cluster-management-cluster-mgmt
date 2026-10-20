using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    /// <summary>
    /// Every deploy command carries its own correlation id, as the Cluster Manager, the suite start and other modules
    /// send it; one deployment per cluster runs, and every other one sent meanwhile is rejected.
    /// </summary>
    public class Parallel : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_reject_a_second_deployment_while_one_updates_the_deployments()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var (_, target) = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await harness.Start();

            var first = Guid.NewGuid();
            await harness.DeployCluster(first, _clusterId, target.Version);
            await harness.PublishClusterNodeAdded(first, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(first, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(first, x => x.UpdatingDeployments);

            // Act
            var second = Guid.NewGuid();
            await harness.DeployCluster(second, _clusterId, _activeVersion);

            // Assert
            await harness.AssertDeployClusterRejected(second, _activeVersion);
            (await SagaHarness(harness).Exists(second)).Should().BeNull("a second deployment of the same cluster must not start");
            await harness.AssertDeployClusterState(first, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Should_reject_a_second_deployment_while_one_commits_its_version()
        {
            // Arrange: the first deployment stores its version before deploying it
            await using var provider = SetupServiceProvider();
            SetupSagaClusterActiveVersionOnly(provider);
            var options = SetupDeployOptions(SetupCommitClusterInfos(VersionIncrementKind.Patch));
            var harness = provider.GetTestHarness();
            await harness.Start();

            var first = Guid.NewGuid();
            await harness.DeployCluster(first, _clusterId, null, options);
            await harness.AssertDeployClusterState(first, x => x.CommittingCluster);

            // Act
            var second = Guid.NewGuid();
            await harness.DeployCluster(second, _clusterId, _activeVersion);

            // Assert
            await harness.AssertDeployClusterRejected(second, _activeVersion);
            (await SagaHarness(harness).Exists(second)).Should().BeNull("a second deployment of the same cluster must not start");
            await harness.AssertDeployClusterState(first, x => x.CommittingCluster);
        }

        [Fact]
        public async Task Should_roll_back_to_the_version_that_is_recorded_as_running_when_another_version_was_sent_meanwhile()
        {
            // Arrange: v1.0.0 runs; user A deploys v1.2.1 and user B deploys v1.4.1 before A's start is reported
            await using var provider = SetupServiceProvider();
            var (active, target) = SetupSagaClusterWithTargetOnSameNode(provider);
            var other = ClusterDbContextTools.CreateCluster(_clusterId, _deployAttemptVersion);
            AddClusterApplication(AddNodeGroup(other, _nodeGroupId), _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);
            _snapshotStore.Load(_clusterId, _deployAttemptVersion, Arg.Any<CancellationToken>()).Returns(other);
            foreach (var cluster in new[] { active, target, other })
                _snapshotStore.LoadCompressedJson(_clusterId, cluster.Version, Arg.Any<CancellationToken>()).Returns(ClusterSerializer.Compress(cluster));

            var harness = provider.GetTestHarness();
            await harness.Start();

            var deploymentA = Guid.NewGuid();
            var deploymentB = Guid.NewGuid();
            await harness.DeployCluster(deploymentA, _clusterId, target.Version);
            await harness.DeployCluster(deploymentB, _clusterId, _deployAttemptVersion);
            await harness.AssertDeployClusterRejected(deploymentB, _deployAttemptVersion);
            await harness.AssertDeployClusterState(deploymentA, x => x.UpdatingDeployments);

            // Act: A's update fails on the node
            await harness.PublishClusterNodeDeploymentsUpdateFailed(deploymentA, _clusterId, _applicationId, target.Version);
            await harness.AssertDeployClusterState(deploymentA, x => x.RollingBackDeployments);

            // Assert: the engines go back to the version that is recorded as running, and B never reached them
            var rollback = await LastUpdateDeploymentsVersion(harness, deploymentA);
            var recorded = provider.GetRequiredService<IClusterDbContext>().ActiveClusters.Single(c => c.Id == _clusterId).Version;

            rollback.Should().Be(recorded);
            (await harness.Published.Any<UpdateDeployments>(m => m.Context.Message.CorrelationId == deploymentB, TestContext.Current.CancellationToken))
                .Should().BeFalse("the rejected deployment must not reach the engines");
        }

        [Fact]
        public async Task Should_reject_a_deployment_that_deletes_the_previous_version_while_the_startup_redeploy_runs_and_accept_it_afterwards()
        {
            // Arrange: the suite redeploys the running version at startup
            await using var provider = SetupServiceProviderWithRemoveCluster();
            var (active, target) = SetupSagaClusterWithTargetOnSameNode(provider);
            _snapshotStore.Exists(_clusterId, Arg.Any<Version>()).Returns(true);

            var harness = provider.GetTestHarness();
            await harness.Start();

            var startup = Guid.NewGuid();
            await harness.DeployCluster(startup, _clusterId, active.Version);
            await harness.AssertDeployClusterState(startup, x => x.UpdatingDeployments);

            // Act: another module deploys a new version and deletes the previous one meanwhile, then once more afterwards
            var tooEarly = Guid.NewGuid();
            await harness.DeployCluster(tooEarly, _clusterId, target.Version, deletePreviousVersion: true);
            await harness.AssertDeployClusterRejected(tooEarly, target.Version);

            await harness.PublishClusterNodeDeploymentsUpdated(startup, _clusterId, _applicationId, active.Version);
            await harness.AssertClusterUpdateCompleted(startup, active.Version);

            var retry = Guid.NewGuid();
            await harness.DeployCluster(retry, _clusterId, target.Version, deletePreviousVersion: true);
            await harness.AssertDeployClusterState(retry, x => x.UpdatingDeployments);
            await harness.PublishClusterNodeDeploymentsUpdated(retry, _clusterId, _applicationId, target.Version);
            await harness.AssertClusterUpdateCompleted(retry, target.Version);

            // Assert
            var sagaHarness = SagaHarness(harness);
            (await sagaHarness.NotExists(startup)).Should().BeNull();
            (await sagaHarness.NotExists(retry)).Should().BeNull("the deployment must not stay in CleaningCluster");
            provider.GetRequiredService<IClusterDbContext>().ActiveClusters.Single(c => c.Id == _clusterId).Version.Should().Be(target.Version);
            _snapshotStore.Received(1).Delete(_clusterId, active.Version);
        }

        private ServiceProvider SetupServiceProviderWithRemoveCluster()
            => new ServiceCollection()
                .AddLogging()
                .ConfigureDeployClusterSaga(_snapshotStore, x =>
                {
                    x.AddSagaStateMachine<DeployClusterStateMachine, DeployClusterState>();
                    x.AddSingleton(_logger);
                    x.AddConsumer<RemoveClusterConsumer>()
                        .Endpoint(e => e.Name = MessagingHelper.GetCommandEndpointName(null, typeof(RemoveCluster), null));
                })
                .BuildServiceProvider(true);

        private static ISagaStateMachineTestHarness<DeployClusterStateMachine, DeployClusterState> SagaHarness(ITestHarness harness)
            => harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();

        private static async Task<Version> LastUpdateDeploymentsVersion(ITestHarness harness, Guid correlationId)
        {
            var sent = await harness.Published.SelectAsync<UpdateDeployments>(TestContext.Current.CancellationToken)
                .Where(m => m.Context.Message.CorrelationId == correlationId)
                .ToListAsync(TestContext.Current.CancellationToken);
            var cluster = await ClusterSerializer.Decompress(sent[^1].Context.Message.CompressedTargetCluster, cancellationToken: TestContext.Current.CancellationToken);
            return cluster.Version;
        }
    }
}
