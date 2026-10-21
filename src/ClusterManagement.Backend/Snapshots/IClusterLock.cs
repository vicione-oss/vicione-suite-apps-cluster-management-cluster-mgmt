namespace ClusterManagement.Backend.Snapshots;

/// <summary>
/// Serializes the changes of a cluster's snapshots within the process, from reading the snapshots through storing
/// the change, and keeps the versions reserved for a commit that is still to come.
/// </summary>
/// <remarks>
/// The lock is not reentrant. Hold it only around reading and writing snapshots: never across sending or publishing
/// a message, so a slow receiver does not hold up the cluster's other changes.
/// The startup seed and the snapshot migration store without it, before any message is consumed. A deployment that
/// changes the active version doesn't take it either.
/// </remarks>
public interface IClusterLock
{
    /// <summary>
    /// Waits until no other change of <paramref name="clusterId"/> runs; disposing the result ends this change.
    /// </summary>
    Task<IDisposable> Acquire(Guid clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps <paramref name="version"/> for a commit sent later, e.g. by the deploy saga, so other commits don't take it.
    /// </summary>
    void Reserve(Guid clusterId, Version version);

    /// <summary>
    /// Releases a version reserved with <see cref="Reserve"/>; does nothing if it isn't reserved.
    /// </summary>
    void Release(Guid clusterId, Version version);

    /// <summary>
    /// The versions of <paramref name="clusterId"/> currently reserved.
    /// </summary>
    IReadOnlyCollection<Version> GetReservedVersions(Guid clusterId);

    /// <summary>
    /// Marks <paramref name="version"/> as being stored, also for a caller that doesn't hold the lock; disposing the
    /// result ends the store. <see langword="null"/> if another store of the version runs.
    /// </summary>
    IDisposable? TryBeginStore(Guid clusterId, Version version);
}
