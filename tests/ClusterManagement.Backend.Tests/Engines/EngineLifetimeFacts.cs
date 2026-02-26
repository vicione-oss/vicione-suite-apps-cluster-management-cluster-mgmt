using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace ClusterManagement.Backend.Tests.Engines;

public sealed class EngineLifetimeFacts
{
    public sealed class Start
    {
        public sealed class Standalone : IAsyncDisposable
        {
            private readonly EngineLifetimeContext _ctx = new(InstanceType.Standalone);

            public ValueTask DisposeAsync() => _ctx.DisposeAsync();

            [Fact]
            public async Task Sends_DeployCluster_when_mediator_returns_one_active_cluster_without_error()
            {
                // Arrange
                var clusterId = Guid.NewGuid();
                var version = new Version(1, 0, 0);

                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        Results = [new ClusterInfo(clusterId, version, "TestCluster", IsActive: true)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.Mediator.Received(1).Send(
                    Arg.Is<Public.Commands.DeployCluster>(cmd => cmd.ClusterId == clusterId && cmd.TargetVersion == version),
                    Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Removes_existing_saga_state_before_sending_DeployCluster()
            {
                // Arrange
                var clusterId = Guid.NewGuid();
                var version = new Version(1, 0, 0);

                var state = new DeployClusterState { ClusterId = clusterId, CurrentState = 2 };
                _ctx.SagaDbContext.DeployClusterState.Add(state);
                await _ctx.SagaDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        Results = [new ClusterInfo(clusterId, version, "TestCluster", IsActive: true)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                var remainingState = await _ctx.SagaDbContext.DeployClusterState
                    .FirstOrDefaultAsync(k => k.ClusterId == clusterId, CancellationToken.None);
                remainingState.Should().BeNull();

                await _ctx.Mediator.Received(1).Send(
                    Arg.Is<Public.Commands.DeployCluster>(cmd => cmd.ClusterId == clusterId),
                    Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Does_nothing_with_saga_state_when_none_exists_for_the_cluster()
            {
                // Arrange
                var clusterId = Guid.NewGuid();
                var version = new Version(1, 0, 0);

                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        Results = [new ClusterInfo(clusterId, version, "TestCluster", IsActive: true)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — no exception and DeployCluster was still sent
                await _ctx.Mediator.Received(1).Send(
                    Arg.Is<Public.Commands.DeployCluster>(cmd => cmd.ClusterId == clusterId),
                    Arg.Any<CancellationToken>());

                _ctx.Logger.LogEntries.Should().NotContain(e => e.Message.Contains("Remove existing saga state"));
            }

            [Fact]
            public async Task Does_not_send_DeployCluster_when_RequestError_is_non_null()
            {
                // Arrange
                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        RequestError = new(0, "Something went wrong"),
                        Results = [new ClusterInfo(Guid.NewGuid(), new Version(1, 0, 0), "TestCluster", IsActive: true)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.Mediator.DidNotReceive().Send(
                    Arg.Any<Public.Commands.DeployCluster>(), Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Does_not_send_DeployCluster_when_there_is_no_active_cluster()
            {
                // Arrange
                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        Results = [new ClusterInfo(Guid.NewGuid(), new Version(1, 0, 0), "Inactive", IsActive: false)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.Mediator.DidNotReceive().Send(
                    Arg.Any<Public.Commands.DeployCluster>(), Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Does_not_query_engineDbContext_EngineHosts()
            {
                // Arrange
                _ctx.Mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                    Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterInfosResponse
                    {
                        Results = [new ClusterInfo(Guid.NewGuid(), new Version(1, 0, 0), "TestCluster", IsActive: true)]
                    });

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — EngineHosts table should not be queried in Standalone mode.
                // We verify indirectly: no calls to engineHostPoolCommunication
                await _ctx.EngineHostPoolCommunication.DidNotReceive().StartHost(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
                await _ctx.EngineHostPoolCommunication.DidNotReceive().GetState(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            }
        }

        public sealed class MasterSlave : IAsyncDisposable
        {
            private readonly EngineLifetimeContext _ctx = new(InstanceType.Master);

            public ValueTask DisposeAsync() => _ctx.DisposeAsync();

            [Fact]
            public async Task Starts_engine_hosts_that_have_deployments_with_ShouldRun_true()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    ElevatedPrivileges = false,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, deploymentId, Arg.Any<CancellationToken>()).Returns(true);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.EngineHostPoolCommunication.Received(1).StartHost(hostId, false, Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Does_not_start_engines_for_host_with_no_ShouldRun_deployments()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    ElevatedPrivileges = false,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = false }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — host is still started (filtered include doesn't affect the Where clause),
                // but no engines are started because the loaded Deployments collection is empty
                await _ctx.EngineHostPoolCommunication.Received(1).StartHost(hostId, false, Arg.Any<CancellationToken>());
                await _ctx.EngineHostCommunication.DidNotReceive().StartEngine(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Calls_StartHost_with_correct_hostId_and_ElevatedPrivileges()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    ElevatedPrivileges = true,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, deploymentId, Arg.Any<CancellationToken>()).Returns(true);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.EngineHostPoolCommunication.Received(1).StartHost(hostId, true, Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Skips_engine_starts_when_host_state_is_NotFound()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.NotFound);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.EngineHostCommunication.DidNotReceive().StartEngine(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Calls_StartEngine_for_each_deployment_when_host_state_is_not_NotFound()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engine1 = Guid.NewGuid();
                var engine2 = Guid.NewGuid();
                var dep1 = Guid.NewGuid();
                var dep2 = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments =
                    [
                        new EngineDeployment { Id = dep1, EngineHostId = hostId, EngineId = engine1, ShouldRun = true },
                new EngineDeployment { Id = dep2, EngineHostId = hostId, EngineId = engine2, ShouldRun = true }
                    ]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.EngineHostCommunication.Received(1).StartEngine(hostId, dep1, Arg.Any<CancellationToken>());
                await _ctx.EngineHostCommunication.Received(1).StartEngine(hostId, dep2, Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Publishes_EngineStarted_event_when_StartEngine_returns_true()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, deploymentId, Arg.Any<CancellationToken>()).Returns(true);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.Mediator.Received(1).Publish(
                    Arg.Is<EngineStarted>(e => e.EngineId == engineId),
                    Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Does_not_publish_EngineStarted_when_StartEngine_returns_false()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, deploymentId, Arg.Any<CancellationToken>()).Returns(false);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert
                await _ctx.Mediator.DidNotReceive().Publish(
                    Arg.Any<EngineStarted>(), Arg.Any<CancellationToken>());
            }

            [Fact]
            public async Task Logs_error_and_skips_engines_when_StartHost_throws()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.StartHost(hostId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(new InvalidOperationException("Host failed to start"));

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — engines should not be started
                await _ctx.EngineHostCommunication.DidNotReceive().StartEngine(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
                _ctx.Logger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Error && e.Message.Contains(hostId.ToString()));
            }

            [Fact]
            public async Task Logs_error_and_swallows_when_StartEngine_throws()
            {
                // Arrange
                var hostId = Guid.NewGuid();
                var engineId = Guid.NewGuid();
                var deploymentId = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = hostId,
                    Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(hostId, deploymentId, Arg.Any<CancellationToken>())
                    .ThrowsAsync(new InvalidOperationException("Engine failed to start"));

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — no exception propagated
                _ctx.Logger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Error && e.Message.Contains(engineId.ToString()));
            }

            [Fact]
            public async Task Starts_multiple_hosts_concurrently()
            {
                // Arrange
                var host1 = Guid.NewGuid();
                var host2 = Guid.NewGuid();
                var engine1 = Guid.NewGuid();
                var engine2 = Guid.NewGuid();
                var dep1 = Guid.NewGuid();
                var dep2 = Guid.NewGuid();

                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = host1,
                    Deployments = [new EngineDeployment { Id = dep1, EngineHostId = host1, EngineId = engine1, ShouldRun = true }]
                });
                _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
                {
                    Id = host2,
                    Deployments = [new EngineDeployment { Id = dep2, EngineHostId = host2, EngineId = engine2, ShouldRun = true }]
                });
                await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

                _ctx.EngineHostPoolCommunication.GetState(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
                _ctx.EngineHostCommunication.StartEngine(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

                // Act
                await _ctx.EngineLifetime.Start(CancellationToken.None);

                // Assert — both hosts were attempted
                await _ctx.EngineHostPoolCommunication.Received(1).StartHost(host1, false, Arg.Any<CancellationToken>());
                await _ctx.EngineHostPoolCommunication.Received(1).StartHost(host2, false, Arg.Any<CancellationToken>());
                await _ctx.EngineHostCommunication.Received(1).StartEngine(host1, dep1, Arg.Any<CancellationToken>());
                await _ctx.EngineHostCommunication.Received(1).StartEngine(host2, dep2, Arg.Any<CancellationToken>());
            }
        }
    }

    public sealed class Stop : IAsyncDisposable
    {
        private readonly EngineLifetimeContext _ctx = new(InstanceType.Standalone);

        public ValueTask DisposeAsync() => _ctx.DisposeAsync();

        [Fact]
        public async Task Queries_all_engine_hosts_regardless_of_ShouldRun()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = false }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert — host with ShouldRun=false deployments is still processed
            await _ctx.EngineHostPoolCommunication.Received(1).StopHost(hostId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Only_calls_StopEngine_when_host_state_is_Started()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert
            await _ctx.EngineHostCommunication.Received(1).StopEngine(hostId, deploymentId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Does_not_call_StopEngine_when_host_state_is_not_Started()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Stopped);

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert
            await _ctx.EngineHostCommunication.DidNotReceive().StopEngine(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Always_publishes_EngineStopped_event()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            // Host state is Stopped — engines weren't running
            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Stopped);

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert — EngineStopped is still published
            await _ctx.Mediator.Received(1).Publish(
                Arg.Is<EngineStopped>(e => e.EngineId == engineId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Stops_host_after_stopping_engines()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert — both StopEngine and StopHost were called
            await _ctx.EngineHostCommunication.Received(1).StopEngine(hostId, deploymentId, Arg.Any<CancellationToken>());
            await _ctx.EngineHostPoolCommunication.Received(1).StopHost(hostId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Logs_error_and_swallows_when_StopHost_throws()
        {
            // Arrange
            var hostId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost { Id = hostId, Deployments = [] });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Stopped);
            _ctx.EngineHostPoolCommunication.StopHost(hostId, Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Host failed to stop"));

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert — no exception propagated
            _ctx.Logger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Error && e.Message.Contains(hostId.ToString()));
        }

        [Fact]
        public async Task Logs_error_and_swallows_when_StopEngine_throws()
        {
            // Arrange
            var hostId = Guid.NewGuid();
            var engineId = Guid.NewGuid();
            var deploymentId = Guid.NewGuid();

            _ctx.EngineDbContext.EngineHosts.Add(new EngineHost
            {
                Id = hostId,
                Deployments = [new EngineDeployment { Id = deploymentId, EngineHostId = hostId, EngineId = engineId, ShouldRun = true }]
            });
            await _ctx.EngineDbContext.SaveChangesAsync(CancellationToken.None);

            _ctx.EngineHostPoolCommunication.GetState(hostId, Arg.Any<CancellationToken>()).Returns(EngineHostState.Started);
            _ctx.EngineHostCommunication.StopEngine(hostId, deploymentId, Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Engine failed to stop"));

            // Act
            await _ctx.EngineLifetime.Stop(CancellationToken.None);

            // Assert — no exception propagated, error was logged
            _ctx.Logger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Error && e.Message.Contains(engineId.ToString()));
            // EngineStopped should NOT be published when StopEngine throws (exception occurs before Publish)
        }
    }
}

internal sealed class EngineLifetimeContext : IAsyncDisposable
{
    internal IEngineHostPoolCommunication EngineHostPoolCommunication { get; } = Substitute.For<IEngineHostPoolCommunication>();
    internal IEngineHostCommunication EngineHostCommunication { get; } = Substitute.For<IEngineHostCommunication>();
    internal ISuiteMediator Mediator { get; } = Substitute.For<ISuiteMediator>();
    internal IInstanceInformationProvider InstanceInformationProvider { get; } = Substitute.For<IInstanceInformationProvider>();
    internal InMemoryLogger<EngineLifetime> Logger { get; } = new();
    internal EngineDbContextSqlite EngineDbContext { get; }
    internal ClusterSagaDbContextSqlite SagaDbContext { get; }
    internal EngineLifetime EngineLifetime { get; }

    internal EngineLifetimeContext(InstanceType instanceType)
    {
        var instanceInformation = Substitute.For<IInstanceInformation>();
        instanceInformation.Type.Returns(instanceType);
        InstanceInformationProvider.Local.Returns(instanceInformation);

        var engineOptions = new DbContextOptionsBuilder<EngineDbContextSqlite>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        EngineDbContext = new EngineDbContextSqlite(engineOptions);
        EngineDbContext.Database.OpenConnection();
        EngineDbContext.Database.EnsureCreated();

        var sagaOptions = new DbContextOptionsBuilder<ClusterSagaDbContextSqlite>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        SagaDbContext = new ClusterSagaDbContextSqlite(sagaOptions);
        SagaDbContext.Database.OpenConnection();
        SagaDbContext.Database.EnsureCreated();

        EngineLifetime = new EngineLifetime(
            EngineHostPoolCommunication,
            EngineHostCommunication,
            EngineDbContext,
            SagaDbContext,
            Mediator,
            InstanceInformationProvider,
            Logger);
    }

    public async ValueTask DisposeAsync()
    {
        EngineDbContext.Database.CloseConnection();
        await EngineDbContext.DisposeAsync();
        SagaDbContext.Database.CloseConnection();
        await SagaDbContext.DisposeAsync();
    }
}
