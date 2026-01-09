using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    private readonly Guid _clusterId = Guid.NewGuid();
    private readonly Version _activeVersion = new(1, 0, 0);
    private readonly Version _targetVersion = new(1, 0, 1);
    private readonly Version _deployAttemptVersion = new(1, 4, 1);

    private readonly Guid _nodeGroupId = Guid.NewGuid();
    private readonly Guid _nodeIdToClean = Guid.Parse("00000000-1111-414A-90BE-6F4BA877A9CF");
    private readonly Guid _nodeIdToAdd = Guid.Parse("AAAAAAAA-1111-414A-90BE-6F4BA877A9CF");
    private readonly Guid _applicationId = Guid.Parse("AAAAAAAA-2222-414A-90BE-6F4BA877A9CF");

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .ConfigureDeployClusterSaga(_snapshotStore, x =>
            {
                x.AddSagaStateMachine<DeployClusterStateMachine, DeployClusterState>();
            })
            .BuildServiceProvider(true);

    private (Cluster, Cluster) SetupSagaDbCluster(IServiceProvider services, IClusterSnapshotStore store)
            => services.PrepareDeployClusterSaga(store, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToClean, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

    private DeployClusterOptions SetupDeployOptions()
    {
        var targetCluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
        var targetGroup = AddNodeGroup(targetCluster, _nodeGroupId);
        AddClusterApplication(targetGroup, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

        _snapshotStore.Load(_clusterId, _targetVersion, Arg.Any<CancellationToken>())
            .Returns(targetCluster);

        var targetCompressed = ClusterSerializer.Compress(targetCluster);

        _snapshotStore.LoadCompressedJson(_clusterId, _targetVersion, Arg.Any<CancellationToken>())
            .Returns(targetCompressed);

        return new DeployClusterOptions(targetCompressed);
    }

    public class Initially : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_initialize_saga_on_deploy_cluster_command()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_initialize_optional_target_cluster()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Transition_to_adding_cluster_nodes_if_nodes_need_to_be_created()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_do_transition_to_update_deployments_if_nodes_dont_need_to_be_created()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = provider.PrepareDeployClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Should_fail_if_no_active_cluster_is_available()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_clusterId, _targetVersion);
        }

        [Fact]
        public async Task Should_fail_if_no_target_cluster_is_available()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_clusterId, _targetVersion);
        }

        [Fact]
        public async Task Should_fail_if_options_cluster_is_invalid()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = new DeployClusterOptions([0x20, 0x22, 0x25, 0x27, 0x16, 0x21]);

            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.PublishClusterChangeFailed(_clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_clusterId, null);
        }
    }

    public class CommittingClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_send_commit_cluster_command_if_options_contained_compressed_json()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = SetupDeployOptions();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.AssertDeployClusterState(_clusterId, x => x.CommittingCluster);

            // Assert            
            await harness.Sent.Any<CommitClusterChange>(k => k.Context.Message.MetaInfo.ClusterId == _clusterId);
        }

        [Fact]
        public async Task Should_fail_on_commit_cluster_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = SetupDeployOptions();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.PublishClusterChangeFailed(_clusterId, _targetVersion);

            // Assert            
            await harness.AssertClusterUpdateFailed(_clusterId, _targetVersion);
        }

        [Fact]
        public async Task Should_do_transition_to_adding_cluster_nodes_when_commit_was_successfull()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToClean, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = SetupDeployOptions();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.PublishClusterChanged(_clusterId, _targetVersion);

            // Assert            
            await harness.AssertDeployClusterState(_clusterId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_do_transition_to_updating_deployments_when_commit_was_successfull()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                // active node == target node -> no adding cluster nodes
                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = SetupDeployOptions();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.PublishClusterChanged(_clusterId, _targetVersion);

            // Assert            
            await harness.AssertDeployClusterState(_clusterId, x => x.UpdatingDeployments);
        }
    }

    public class AddingClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Transition_to_update_deployments_when_all_nodes_are_running()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_adding_cluster_nodes()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.AssertDeployClusterState(_clusterId, x => x.AddingClusterNodes);

            // Act
            await harness.DeployCluster(_clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_clusterId, _deployAttemptVersion);
        }
    }

    public class UpdatingDeployments : DeployClusterStateMachineTests
    {
        private async Task TransitionToUpdatingDeployments(ITestHarness harness, Version targetVersion)
        {
            await harness.Start();
            await harness.DeployCluster(_clusterId, targetVersion);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.AssertDeployClusterState(_clusterId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Transition_to_cleaning_cluster_nodes_when_all_deployments_are_updated_and_nodes_need_cleanup()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.CleaningClusterNodes);
        }

        [Fact]
        public async Task Transition_to_rolling_back_deployments_when_at_least_one_update_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_clusterId, _applicationId);

            // Assert
            await harness.AssertDeployClusterState(_clusterId, x => x.RollingBackDeployments);
        }

        [Fact]
        public async Task Finalize_when_all_deployents_are_updated_and_nodes_dont_need_cleanup()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = provider.PrepareDeployClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateCompleted(_clusterId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_updating_deployments()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.DeployCluster(_clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_clusterId, _deployAttemptVersion);
        }
    }

    public class CleaningClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Finalize_when_all_nodes_are_cleaned_up()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);
            await harness.AssertDeployClusterState(_clusterId, x => x.CleaningClusterNodes);

            // Act
            await harness.PublishClusterNodeCleaned(_clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_clusterId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_cleaning_cluster_nodes()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);
            await harness.AssertDeployClusterState(_clusterId, x => x.CleaningClusterNodes);

            // Act
            await harness.DeployCluster(_clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_clusterId, _deployAttemptVersion);
        }
    }

    public class RollingBackDeployments : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Finalize_when_all_nodes_are_rolled_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_clusterId, _applicationId);
            await harness.AssertDeployClusterState(_clusterId, x => x.RollingBackDeployments);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateFailed(_clusterId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_rolling_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_clusterId, _applicationId);
            await harness.AssertDeployClusterState(_clusterId, x => x.RollingBackDeployments);

            // Act
            await harness.DeployCluster(_clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_clusterId, _deployAttemptVersion);
        }
    }

    public class DeployStandaloneCluster : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_complete_node_application_deployment()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);
            await harness.PublishClusterNodeCleaned(_clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_clusterId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_fail_if_at_least_one_deployment_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbCluster(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);

            // -> trigger rollback
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_clusterId, _applicationId);

            // -> complete rollback
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateFailed(_clusterId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_process_target_cluster_from_command_options()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var options = SetupDeployOptions();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, _targetVersion, options);
            await harness.PublishClusterChanged(_clusterId, _targetVersion);
            await harness.PublishClusterNodeAdded(_clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _applicationId);
            await harness.PublishClusterNodeCleaned(_clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_clusterId, null);
        }
    }
}
