using ClusterManagement.Backend.DeployCluster;
using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public class ClusterNodeDeploymentPlannedConsumerTests
{
    [Fact]
    public async Task Keeps_a_published_plan_for_the_deployment_state()
    {
        // Arrange
        DeploymentPlanStore store = new();
        ClusterNodeDeploymentPlannedConsumer sut = new(store);
        ClusterNodeDeploymentPlanned plan = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [new ClusterNodeDeploymentPlannedStep(ClusterNodeDeploymentStep.Prepare)]);
        var context = Substitute.For<ConsumeContext<ClusterNodeDeploymentPlanned>>();
        context.Message.Returns(plan);

        // Act
        await sut.Consume(context);

        // Assert
        store.Of(plan.ClusterId, plan.CorrelationId).Should().ContainSingle().Which.Should().BeSameAs(plan);
    }

    [Fact]
    public void Receives_the_plans_on_every_instance_on_an_endpoint_of_its_own()
    {
        // Arrange
        IConsumerDefinition sut = new ClusterNodeDeploymentPlannedConsumerDefinition();
        IConsumerDefinition otherInstance = new ClusterNodeDeploymentPlannedConsumerDefinition();

        // Act
        var endpointName = sut.GetEndpointName(DefaultEndpointNameFormatter.Instance);

        // Assert
        typeof(ClusterNodeDeploymentPlannedConsumer).IsReadOnlyConsumer().Should().BeTrue();
        endpointName.Should().Contain("DeploymentPlans").And.NotBe(otherInstance.GetEndpointName(DefaultEndpointNameFormatter.Instance));
        sut.EndpointDefinition!.IsTemporary.Should().BeTrue();
    }
}
