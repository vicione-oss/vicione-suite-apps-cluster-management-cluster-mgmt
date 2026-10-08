using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class EngineHostRuntimeModelTests
{
    [Fact]
    public void Ties_every_engine_to_the_instance_running_its_host()
    {
        // Arrange
        var engine = new Engine { Id = Guid.NewGuid(), Name = "Scanner", MinCycleTime = 100 };
        var host = new EngineHost { Id = Guid.NewGuid(), Name = "DCW-Host", ElevatedPrivileges = true, Engines = [engine] };
        var instance = new ClusterApplication { Id = Guid.NewGuid(), EngineHosts = [host] };
        var cluster = new Cluster { NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Applications = [instance] }] }] };

        // Act
        var hosts = EngineHostRuntimeModel.FromCluster(cluster);

        // Assert
        var model = hosts.Should().ContainSingle().Which;
        model.Should().Match<EngineHostRuntimeModel>(h => h.Id == host.Id && h.InstanceId == instance.Id && h.Name == "DCW-Host" && h.ElevatedPrivileges);
        model.Engines.Should().ContainSingle()
            .Which.Should().Be(new EngineRuntimeModel(instance.Id, engine.Id, "Scanner", 100));
    }

    [Fact]
    public void Leaves_out_engine_hosts_the_deployment_does_not_deploy()
    {
        // Arrange
        var instance = new ClusterApplication { EngineHosts = [new EngineHost { Engines = [new Engine()] }] };
        var nested = new ClusterNodeGroup { Nodes = [new ClusterNode { Applications = [instance] }] };
        var cluster = new Cluster { NodeGroups = [new ClusterNodeGroup { NodeGroups = [nested] }] };

        // Act
        var hosts = EngineHostRuntimeModel.FromCluster(cluster);

        // Assert
        hosts.Should().BeEmpty();
    }

    [Fact]
    public void Engine_state_is_unknown_until_queried()
    {
        // Arrange
        var host = new EngineHost { Engines = [new Engine()] };
        var cluster = new Cluster { NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Applications = [new ClusterApplication { EngineHosts = [host] }] }] }] };

        // Act
        var engine = EngineHostRuntimeModel.FromCluster(cluster)[0].Engines[0];

        // Assert
        engine.IsChecked.Should().BeFalse();
        engine.State.Should().BeNull();
        engine.IsReachable.Should().BeFalse();
        engine.IsRunning.Should().BeFalse();
        engine.DeploymentId.Should().BeNull();
    }

    [Fact]
    public void The_cycle_time_of_a_host_is_the_cycle_time_of_its_first_engine()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var host = new EngineHostRuntimeModel(Guid.NewGuid(), instanceId, "Host", false,
        [
            new EngineRuntimeModel(instanceId, Guid.NewGuid(), "Fast", 100),
            new EngineRuntimeModel(instanceId, Guid.NewGuid(), "Slow", 250),
        ]);

        // Act
        var cycleTime = host.CycleTime;

        // Assert
        cycleTime.Should().Be(100);
    }

    [Fact]
    public void WithEngine_replaces_only_the_engine_with_the_same_id()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var first = new EngineRuntimeModel(instanceId, Guid.NewGuid(), "First", 100);
        var second = new EngineRuntimeModel(instanceId, Guid.NewGuid(), "Second", 100);
        var other = new EngineRuntimeModel(instanceId, Guid.NewGuid(), "Other", 100);
        IReadOnlyList<EngineHostRuntimeModel> hosts =
        [
            new(Guid.NewGuid(), instanceId, "Host", false, [first, second]),
            new(Guid.NewGuid(), instanceId, "Other host", false, [other]),
        ];
        var checkedSecond = second with { IsChecked = true, State = EngineState.Running };

        // Act
        var result = EngineHostRuntimeModel.WithEngine(hosts, checkedSecond);

        // Assert
        result[0].Engines.Should().Equal(first, checkedSecond);
        result[1].Engines.Should().Equal(other);
    }
}
