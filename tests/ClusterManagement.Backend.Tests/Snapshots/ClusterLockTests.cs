using ClusterManagement.Backend.Snapshots;

namespace ClusterManagement.Backend.Tests.Snapshots;

public class ClusterLockTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    private readonly ClusterLock _clusterLock = new();
    private readonly Guid _clusterId = Guid.NewGuid();

    [Fact]
    public async Task Acquire_should_wait_for_the_change_of_the_same_cluster()
    {
        // Arrange
        var first = await _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);

        // Act
        var second = _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);

        // Assert
        second.IsCompleted.Should().BeFalse();
        first.Dispose();
        (await second.WaitAsync(s_timeout, TestContext.Current.CancellationToken)).Dispose();
    }

    [Fact]
    public async Task Acquire_should_not_wait_for_another_cluster()
    {
        // Arrange
        using var first = await _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);

        // Act
        var other = _clusterLock.Acquire(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        other.IsCompletedSuccessfully.Should().BeTrue();
        (await other).Dispose();
    }

    [Fact]
    public async Task Disposing_twice_should_end_the_change_once()
    {
        // Arrange
        var first = await _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);
        first.Dispose();
        first.Dispose();
        using var second = await _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);

        // Act
        var third = _clusterLock.Acquire(_clusterId, TestContext.Current.CancellationToken);

        // Assert
        third.IsCompleted.Should().BeFalse("the second change still runs");
        second.Dispose();
        (await third.WaitAsync(s_timeout, TestContext.Current.CancellationToken)).Dispose();
    }

    [Fact]
    public void Reserved_versions_should_be_kept_per_cluster_until_released()
    {
        // Arrange
        var otherClusterId = Guid.NewGuid();
        _clusterLock.Reserve(_clusterId, new Version(1, 0, 2));
        _clusterLock.Reserve(_clusterId, new Version(1, 0, 3));
        _clusterLock.Reserve(otherClusterId, new Version(2, 0, 0));

        // Act
        _clusterLock.Release(_clusterId, new Version(1, 0, 2));
        _clusterLock.Release(_clusterId, new Version(9, 9, 9));

        // Assert
        _clusterLock.GetReservedVersions(_clusterId).Should().Equal(new Version(1, 0, 3));
        _clusterLock.GetReservedVersions(otherClusterId).Should().Equal(new Version(2, 0, 0));
    }

    [Fact]
    public void TryBeginStore_should_refuse_a_version_being_stored_until_its_store_ends()
    {
        // Arrange
        var version = new Version(1, 0, 2);
        var first = _clusterLock.TryBeginStore(_clusterId, version);

        // Act
        var second = _clusterLock.TryBeginStore(_clusterId, version);
        using var otherVersion = _clusterLock.TryBeginStore(_clusterId, new Version(1, 0, 3));
        using var otherCluster = _clusterLock.TryBeginStore(Guid.NewGuid(), version);

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
        otherVersion.Should().NotBeNull();
        otherCluster.Should().NotBeNull();
        first.Dispose();
        using var afterEnd = _clusterLock.TryBeginStore(_clusterId, version);
        afterEnd.Should().NotBeNull();
    }

    [Fact]
    public void Ending_a_store_twice_should_not_end_the_next_store()
    {
        // Arrange
        var version = new Version(1, 0, 2);
        var first = _clusterLock.TryBeginStore(_clusterId, version)!;
        first.Dispose();
        using var second = _clusterLock.TryBeginStore(_clusterId, version);

        // Act
        first.Dispose();

        // Assert
        _clusterLock.TryBeginStore(_clusterId, version).Should().BeNull("the second store still runs");
    }

    [Fact]
    public void Separate_locks_should_not_share_the_versions_being_stored()
    {
        // Arrange
        using var stored = _clusterLock.TryBeginStore(_clusterId, new Version(1, 0, 2));

        // Act
        using var inOtherHost = new ClusterLock().TryBeginStore(_clusterId, new Version(1, 0, 2));

        // Assert
        inOtherHost.Should().NotBeNull();
    }
}
