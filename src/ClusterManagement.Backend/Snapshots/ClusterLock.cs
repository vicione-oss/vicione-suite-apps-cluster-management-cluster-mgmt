using System.Collections.Concurrent;

namespace ClusterManagement.Backend.Snapshots;

/// <summary>
/// In-process <see cref="IClusterLock"/>: one master or standalone process writes the snapshots.
/// The semaphores are kept for the lifetime of the process; there is one per cluster.
/// </summary>
internal sealed class ClusterLock : IClusterLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly Dictionary<Guid, HashSet<Version>> _reservedVersions = [];
    private readonly ConcurrentDictionary<(Guid ClusterId, Version Version), byte> _versionsBeingStored = new();

    public async Task<IDisposable> Acquire(Guid clusterId, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(clusterId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    public void Reserve(Guid clusterId, Version version)
    {
        lock (_reservedVersions)
        {
            if (!_reservedVersions.TryGetValue(clusterId, out var versions))
                _reservedVersions[clusterId] = versions = [];

            versions.Add(version);
        }
    }

    public void Release(Guid clusterId, Version version)
    {
        lock (_reservedVersions)
        {
            if (!_reservedVersions.TryGetValue(clusterId, out var versions) || !versions.Remove(version))
                return;

            if (versions.Count == 0)
                _reservedVersions.Remove(clusterId);
        }
    }

    public IReadOnlyCollection<Version> GetReservedVersions(Guid clusterId)
    {
        lock (_reservedVersions)
            return _reservedVersions.TryGetValue(clusterId, out var versions) ? [.. versions] : [];
    }

    public IDisposable? TryBeginStore(Guid clusterId, Version version)
    {
        var key = (clusterId, version);
        return _versionsBeingStored.TryAdd(key, 0)
            ? new StoreEnd(() => _versionsBeingStored.TryRemove(key, out _))
            : null;
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                semaphore.Release();
        }
    }

    private sealed class StoreEnd(Action end) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                end();
        }
    }
}
