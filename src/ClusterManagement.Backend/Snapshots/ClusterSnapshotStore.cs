using System.IO.Abstractions;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.IO;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

public sealed class ClusterSnapshotStore : IClusterSnapshotStore
{
    private const char Separator = '_';

    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    // Knows the versions being stored: the store is transient, while its directory outlives it
    private readonly IClusterLock _clusterLock;
    private readonly ILogger<ClusterSnapshotStore> _logger;
    private readonly string _storageDirectory;

    public ClusterSnapshotStore(
        IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter,
        IClusterLock clusterLock,
        ILogger<ClusterSnapshotStore> logger)
    {
        _fileSystem = fileSystem;
        _atomicFileWriter = atomicFileWriter;
        _clusterLock = clusterLock;
        _logger = logger;
        _storageDirectory = _fileSystem.GetSnapshotsPath(workspaceProvider);

        //Ensure Directory
        _fileSystem.Directory.CreateDirectory(_storageDirectory);
    }

    public bool IsEmpty()
        => GetSnapshotFiles().Length == 0;

    public void Delete(Guid clusterId, Version clusterVersion)
    {
        DeleteInternal(GetStorageInfoPath(clusterId, clusterVersion));
        DeleteInternal(GetStorageFilePath(clusterId, clusterVersion));

        void DeleteInternal(string filePath)
        {
            if (!_fileSystem.File.Exists(filePath))
                _logger.LogClusterNotDeleted(_fileSystem.Path.GetFileNameWithoutExtension(filePath));

            _fileSystem.File.Delete(filePath);
        }
    }

    public bool Exists(Guid clusterId, Version clusterVersion)
        => _fileSystem.File.Exists(GetStorageFilePath(clusterId, clusterVersion));

    public async Task<IEnumerable<ClusterInfo>> GetClusterInfos(Guid? clusterId, CancellationToken cancellationToken)
    {
        var infoFiles = GetSnapshotFiles(clusterId);
        var result = new List<ClusterInfo>();

        foreach (var infoFile in infoFiles)
        {
            try
            {
                var parts = Path.GetFileNameWithoutExtension(infoFile).Split(Separator);

                // todo: handle case info exists cluster.gz does not?
                if (!Exists(Guid.Parse(parts[0]), new Version(parts[1])))
                    continue;

                var storedInfo = await LoadInfo(infoFile, cancellationToken);

                result.Add(new ClusterInfo(storedInfo.Id, storedInfo.Version, storedInfo.Name)
                {
                    Description = storedInfo.Description,
                });
            }
            catch (Exception e)
            {
                _logger.LogGetClusterInfosError(e, clusterId);
            }
        }
        return result;
    }

    private string[] GetSnapshotFiles(Guid? clusterId = null)
    {
        var searchPattern = clusterId.HasValue ? $"{clusterId.Value}*info.json" : $"*info.json";
        return _fileSystem.Directory
            .GetFiles(_storageDirectory, searchPattern, SearchOption.TopDirectoryOnly);
    }

    private async Task<ClusterSnapshotInfo> LoadInfo(string storageInfoPath, CancellationToken cancellationToken)
    {
        await using var fileStream = _fileSystem.FileStream.New(storageInfoPath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });
        return await JsonSerializer.DeserializeAsync<ClusterSnapshotInfo>(fileStream, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize {nameof(ClusterSnapshotInfo)}");
    }

    private string GetStorageInfoPath(Guid id, Version version)
        => Path.Combine(_storageDirectory, $"{GetStorageInfoName(id, version)}");

    internal static string GetStorageInfoName(Guid id, Version version)
        => $"{GetStorageKey(id, version)}{Separator}info.json";

    private string GetStorageFilePath(Guid id, Version version)
        => Path.Combine(_storageDirectory, $"{GetStorageFileName(id, version)}");

    internal static string GetStorageFileName(Guid id, Version version)
        => $"{GetStorageKey(id, version)}.gz";

    private static string GetStorageKey(Guid id, Version version)
        => $"{id}{Separator}{version}";

    public async Task<Cluster?> Load(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken)
    {
        try
        {
            var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
            if (!_fileSystem.File.Exists(storageFilePath))
            {
                _logger.LogClusterNotLoaded(clusterId, clusterVersion);
                return default;
            }

            await using var fs = _fileSystem.FileStream.New(storageFilePath, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            return await ClusterSerializer.Decompress(fs, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogLoadClusterError(e, clusterId, clusterVersion);
            return null;
        }
    }

    public async Task<byte[]> LoadCompressedJson(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
            if (!_fileSystem.File.Exists(storageFilePath))
            {
                _logger.LogClusterNotLoaded(clusterId, clusterVersion);
                return [];
            }

            return await _fileSystem.File.ReadAllBytesAsync(storageFilePath, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogLoadClusterError(e, clusterId, clusterVersion);
            return [];
        }
    }

    public async Task<T?> Load<T>(Guid clusterId, Version clusterVersion, Func<Stream, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
            if (!_fileSystem.File.Exists(storageFilePath))
            {
                _logger.LogClusterNotLoaded(clusterId, clusterVersion);
                return default;
            }

            await using var fs = _fileSystem.FileStream.New(storageFilePath, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            using GZipStream compressedStream = new(fs, CompressionMode.Decompress);
            return await read(compressedStream, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogLoadClusterError(e, clusterId, clusterVersion);
            return default;
        }
    }

    public Task Store(byte[] compressedClusterJson, ClusterSnapshotInfo info, CancellationToken cancellationToken)
        => StoreVersion(info.Id, info.Version, false, async () =>
        {
            await StoreInfo(info, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, false, cancellationToken);
            await StoreCompressedJson(info.Id, info.Version, compressedClusterJson, false, cancellationToken);
        });

    public Task Store<T>(Guid clusterId, Version clusterVersion, JsonObject info, T value, Func<Stream, T, CancellationToken, Task> write, bool replace, CancellationToken cancellationToken)
    {
        return StoreVersion(clusterId, clusterVersion, replace, async () =>
        {
            await StoreInfoLocal(ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions);
            await StoreJson();
        });

        async Task StoreInfoLocal(JsonSerializerOptions serializerOptions)
            => await _atomicFileWriter.WriteAsync(GetStorageInfoPath(clusterId, clusterVersion),
                stream => JsonSerializer.SerializeAsync(stream, info, serializerOptions, cancellationToken),
                cancellationToken);

        async Task StoreJson()
        {
            var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
            await _atomicFileWriter.WriteAsync(storageFilePath, async stream =>
            {
                // leaveOpen: the atomic writer still flushes the underlying stream after this callback
                await using GZipStream compressedStream = new(stream, CompressionMode.Compress, leaveOpen: true);
                await write(compressedStream, value, cancellationToken);
            }, cancellationToken);
        }
    }

    private async Task StoreCompressedJson(Guid clusterId, Version clusterVersion, byte[] compressedClusterJson, bool replace, CancellationToken cancellationToken)
    {
        var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
        if (!replace && _fileSystem.File.Exists(storageFilePath))
            throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");

        await _atomicFileWriter.WriteAsync(storageFilePath,
            async stream => await stream.WriteAsync(compressedClusterJson, cancellationToken),
            cancellationToken);
    }

    public Task Store(Cluster cluster, CancellationToken cancellationToken)
        => Store(cluster, false, cancellationToken);

    public Task Store(Cluster cluster, bool replace, CancellationToken cancellationToken)
        => StoreVersion(cluster.Id, cluster.Version, replace, async () =>
        {
            // when we store the empty cluster the designs are not yet loaded and therefore the static
            // serializer options get created without types from fbs! In this case we need to create
            // the options later
            var serializerOptions = ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions;
            await StoreInfo(cluster, serializerOptions, replace, cancellationToken);
            await StoreCompressedJson(cluster.Id, cluster.Version, cluster, serializerOptions, replace, cancellationToken);
        });

    /// <summary>
    /// Runs <paramref name="write"/> as the only store of the version in this process, and cleans up after a failed new version.
    /// A replaced version is kept: the atomic writer leaves the file it failed to write as it was, and an info
    /// written before describes the same cluster version.
    /// </summary>
    private async Task StoreVersion(Guid clusterId, Version clusterVersion, bool replace, Func<Task> write)
    {
        // A parallel store of the version fails before it writes, so its cleanup never deletes the files being stored
        using (_clusterLock.TryBeginStore(clusterId, clusterVersion)
            ?? throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} is being stored"))
        {
            if (!replace)
                ThrowIfExists(clusterId, clusterVersion);

            try
            {
                await write();
            }
            catch (Exception e)
            {
                if (!replace)
                    Delete(clusterId, clusterVersion);

                _logger.LogStoreClusterError(e, clusterId, clusterVersion);
                throw;
            }
        }
    }

    private async Task StoreCompressedJson(Guid clusterId, Version clusterVersion, Cluster cluster, JsonSerializerOptions options, bool replace, CancellationToken cancellationToken)
    {
        var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
        if (!replace && _fileSystem.File.Exists(storageFilePath))
            throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");

        await _atomicFileWriter.WriteAsync(storageFilePath, async stream =>
        {
            // leaveOpen: the atomic writer still flushes the underlying stream after this callback
            await using GZipStream gs = new(stream, CompressionMode.Compress, leaveOpen: true);
            await JsonSerializer.SerializeAsync(gs, cluster, options, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Throws before anything is written, so the cleanup of a failed store never deletes an existing version.
    /// Only the snapshot is checked: an info left without its snapshot (e.g. a crash between the two writes) makes
    /// the store fail and its cleanup delete that info, so the next store of the version succeeds.
    /// An info of a version being stored is never taken for such a leftover: <see cref="StoreVersion"/> runs only
    /// one store of a version at a time.
    /// </summary>
    private void ThrowIfExists(Guid clusterId, Version clusterVersion)
    {
        if (Exists(clusterId, clusterVersion))
            throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");
    }

    private Task StoreInfo(Cluster cluster, JsonSerializerOptions serializerOptions, bool replace, CancellationToken cancellationToken)
    {
        var info = new ClusterSnapshotInfo(cluster.Id, cluster.Version, cluster.Name)
        {
            Dependencies = [.. cluster.Dependencies.OrderBy(d => d.Name)],
            Description = cluster.Description,
        };

        return StoreInfo(info, serializerOptions, replace, cancellationToken);
    }

    private async Task StoreInfo(ClusterSnapshotInfo info, JsonSerializerOptions serializerOptions, bool replace, CancellationToken cancellationToken)
    {
        var storageInfoPath = GetStorageInfoPath(info.Id, info.Version);
        if (!replace && _fileSystem.File.Exists(storageInfoPath))
            throw new InvalidOperationException($"Cluster info {info.Id}-{info.Version} already exists");

        await _atomicFileWriter.WriteAsync(storageInfoPath,
            stream => JsonSerializer.SerializeAsync(stream, info, serializerOptions, cancellationToken),
            cancellationToken);
    }
}
