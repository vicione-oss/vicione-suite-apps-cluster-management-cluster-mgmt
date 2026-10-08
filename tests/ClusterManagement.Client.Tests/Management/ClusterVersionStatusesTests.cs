using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Events;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterVersionStatusesTests
{
    [Fact]
    public void Active_version_is_running()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.3", isActive: true), null).Should().Be(ClusterVersionStatus.Running);

    [Fact]
    public void Latest_inactive_version_is_a_draft()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.4", isLatest: true), null).Should().Be(ClusterVersionStatus.Draft);

    [Fact]
    public void Version_zero_one_zero_is_the_default()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.0"), null).Should().Be(ClusterVersionStatus.Default);

    [Fact]
    public void Other_versions_are_previous()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.2"), null).Should().Be(ClusterVersionStatus.Previous);

    [Fact]
    public void Version_being_deployed_is_deploying_even_when_it_is_the_draft()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.4", isLatest: true), Deployment("0.1.4")).Should().Be(ClusterVersionStatus.Deploying);

    [Fact]
    public void Version_whose_deployment_failed_is_failed()
    {
        // Arrange
        var deployment = Deployment("0.1.4");
        deployment.Fail(new ClusterUpdateFailed(Guid.NewGuid(), ClusterInfoModels.ClusterId, new Version("0.1.4")));

        // Act
        var status = ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.4", isLatest: true), deployment);

        // Assert
        status.Should().Be(ClusterVersionStatus.Failed);
    }

    [Fact]
    public void Deployment_of_another_version_does_not_change_the_status()
        => ClusterVersionStatuses.Of(ClusterInfoModels.Version("0.1.3", isActive: true), Deployment("0.1.4")).Should().Be(ClusterVersionStatus.Running);

    private static DeploymentProgressModel Deployment(string targetVersion)
        => DeploymentProgressModel.Start(new ClusterUpdateStarted(Guid.NewGuid(), ClusterInfoModels.ClusterId, new Version(targetVersion)), DateTimeOffset.UnixEpoch);
}
