using ClusterManagement.Backend.DeployCluster;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;

namespace ClusterManagement.Backend.Tests.DeployCluster;

public class DeploymentPlanStoreTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Guid s_instanceId = Guid.NewGuid();

    private readonly DeploymentPlanStore _sut = new();

    [Fact]
    public void Keeps_the_latest_plan_of_each_instance()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var otherInstance = Guid.NewGuid();
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare));
        _sut.Keep(Plan(correlationId, otherInstance, ClusterNodeDeploymentStep.Prepare));

        // Act
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.StartEngine));

        // Assert
        _sut.Of(s_clusterId, correlationId).Select(p => (p.ClusterApplicationId, p.Steps.Count))
            .Should().BeEquivalentTo([(s_instanceId, 2), (otherInstance, 1)]);
    }

    [Fact]
    public void Keeps_the_plans_of_the_running_deployment_when_a_late_plan_of_an_earlier_one_arrives()
    {
        // Arrange
        var earlier = Guid.NewGuid();
        var running = Guid.NewGuid();
        var otherInstance = Guid.NewGuid();
        _sut.Keep(Plan(earlier, s_instanceId, ClusterNodeDeploymentStep.Prepare));
        _sut.Keep(Plan(running, otherInstance, ClusterNodeDeploymentStep.Prepare));

        // Act
        _sut.Keep(Plan(earlier, s_instanceId, ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.StartEngine));

        // Assert
        _sut.Of(s_clusterId, running).Should().ContainSingle().Which.ClusterApplicationId.Should().Be(otherInstance);
        _sut.Of(s_clusterId, earlier).Should().ContainSingle().Which.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Keeps_the_rollback_plan_when_a_late_update_plan_arrives()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare));
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare) with { IsRollback = true });

        // Act
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.StartEngine));

        // Assert
        _sut.Of(s_clusterId, correlationId).Select(p => (p.IsRollback, p.Steps.Count))
            .Should().BeEquivalentTo([(false, 2), (true, 1)]);
    }

    [Fact]
    public void Drops_the_plans_of_the_oldest_deployment_once_a_cluster_has_more_than_two()
    {
        // Arrange
        var oldest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var newest = Guid.NewGuid();
        _sut.Keep(Plan(oldest, s_instanceId, ClusterNodeDeploymentStep.Prepare));
        _sut.Keep(Plan(middle, s_instanceId, ClusterNodeDeploymentStep.Prepare));

        // Act
        _sut.Keep(Plan(newest, s_instanceId, ClusterNodeDeploymentStep.Prepare));

        // Assert
        _sut.Of(s_clusterId, oldest).Should().BeEmpty();
        _sut.Of(s_clusterId, middle).Should().ContainSingle();
        _sut.Of(s_clusterId, newest).Should().ContainSingle();
    }

    [Fact]
    public void Has_no_plan_of_another_cluster()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        _sut.Keep(Plan(correlationId, s_instanceId, ClusterNodeDeploymentStep.Prepare));

        // Act
        var plans = _sut.Of(Guid.NewGuid(), correlationId);

        // Assert
        plans.Should().BeEmpty();
    }

    private static ClusterNodeDeploymentPlanned Plan(Guid correlationId, Guid instanceId, params ClusterNodeDeploymentStep[] steps)
        => new(correlationId, s_clusterId, instanceId, [.. steps.Select(step => new ClusterNodeDeploymentPlannedStep(step))]);
}
