using System.IO.Abstractions;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

public sealed class ClusterSnapshotStore : IClusterSnapshotStore
{
    private const char Separator = '_';

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ClusterSnapshotStore> _logger;
    private readonly string _storageDirectory;

    public ClusterSnapshotStore(
        IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
        IFileSystem fileSystem,
        ILogger<ClusterSnapshotStore> logger)
    {
        _fileSystem = fileSystem;
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
                _logger.LogWarning("Delete cluster {FileName} not found", _fileSystem.Path.GetFileNameWithoutExtension(filePath));

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
                _logger.LogError(e, nameof(GetClusterInfos));
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
                _logger.LogWarning("Load cluster {ClusterId}-{ClusterVersion} not found", clusterId, clusterVersion);
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
            _logger.LogError(e, "Failed to load cluster {ClusterId}-{ClusterVersion}", clusterId, clusterVersion);
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
                _logger.LogWarning("Load cluster {ClusterId}-{ClusterVersion} not found", clusterId, clusterVersion);
                return [];
            }

            return await _fileSystem.File.ReadAllBytesAsync(storageFilePath, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load cluster {ClusterId}-{ClusterVersion}", clusterId, clusterVersion);
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
                _logger.LogWarning("Load cluster {ClusterId}-{ClusterVersion} not found", clusterId, clusterVersion);
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
            _logger.LogError(e, "Failed to load cluster {ClusterId}-{ClusterVersion}", clusterId, clusterVersion);
            return default;
        }
    }

    public async Task Store(byte[] compressedClusterJson, ClusterSnapshotInfo info, CancellationToken cancellationToken)
    {
        try
        {
            await StoreInfo(info, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);
            await StoreCompressedJson(info.Id, info.Version, compressedClusterJson, false, cancellationToken);
        }
        catch (Exception e)
        {
            Delete(info.Id, info.Version);
            _logger.LogError(e, "Failed to store cluster {ClusterId}-{ClusterVersion}", info.Id, info.Version);
            throw;
        }
    }

    public async Task Store<T>(Guid clusterId, Version clusterVersion, JsonObject info, T value, Func<Stream, T, CancellationToken, Task> write, bool replace, CancellationToken cancellationToken)
    {
        try
        {
            await StoreInfoLocal(ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions);
            await StoreJson();
        }
        catch (Exception e)
        {
            Delete(clusterId, clusterVersion);
            _logger.LogError(e, "Failed to store cluster {ClusterId}-{ClusterVersion}", clusterId, clusterVersion);
            throw;
        }

        async Task StoreInfoLocal(JsonSerializerOptions serializerOptions)
        {
            var storageInfoPath = GetStorageInfoPath(clusterId, clusterVersion);
            if (!replace && _fileSystem.File.Exists(storageInfoPath))
                throw new InvalidOperationException($"Cluster info {clusterId}-{clusterVersion} already exists");

            await using var fileStream = _fileSystem.FileStream.New(storageInfoPath, new FileStreamOptions()
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            await JsonSerializer.SerializeAsync(fileStream, info, serializerOptions, cancellationToken);
        }

        async Task StoreJson()
        {
            var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
            if (!replace && !_fileSystem.File.Exists(storageFilePath))
                throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");

            await using var fs = _fileSystem.FileStream.New(storageFilePath, new FileStreamOptions()
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            using GZipStream compressedStream = new(fs, CompressionMode.Compress);
            await write(compressedStream, value, cancellationToken);
        }
    }

    private async Task StoreCompressedJson(Guid clusterId, Version clusterVersion, byte[] compressedClusterJson, bool replace, CancellationToken cancellationToken)
    {
        var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
        if (!replace && _fileSystem.File.Exists(storageFilePath))
            throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");

        await _fileSystem.File.WriteAllBytesAsync(storageFilePath, compressedClusterJson, cancellationToken);
    }

    public async Task Store(Cluster cluster, CancellationToken cancellationToken)
    {
        try
        {
            // when we store the empty cluster the designs are not yet loaded and therefore the static
            // serializer options get created without types from fbs! In this case we need to create
            // the options later
            var serializerOptions = ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions;
            await StoreInfo(cluster, serializerOptions, cancellationToken);
            await StoreCompressedJson(cluster.Id, cluster.Version, cluster, serializerOptions, cancellationToken);
        }
        catch (Exception e)
        {
            Delete(cluster.Id, cluster.Version);
            _logger.LogError(e, "Failed to store cluster {ClusterId}-{ClusterVersion}", cluster.Id, cluster.Version);
            throw;
        }
        return;
    }

    private async Task StoreCompressedJson(Guid clusterId, Version clusterVersion, Cluster cluster, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        var storageFilePath = GetStorageFilePath(clusterId, clusterVersion);
        if (_fileSystem.File.Exists(storageFilePath))
            throw new InvalidOperationException($"Cluster {clusterId}-{clusterVersion} already exists");

        await using var fs = _fileSystem.FileStream.New(storageFilePath, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });
        await using GZipStream gs = new(fs, CompressionMode.Compress);
        await JsonSerializer.SerializeAsync(gs, cluster, options, cancellationToken);
    }

    private Task StoreInfo(Cluster cluster, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        var info = new ClusterSnapshotInfo(cluster.Id, cluster.Version, cluster.Name)
        {
            Dependencies = [.. cluster.Dependencies.OrderBy(d => d.Name)],
            Description = cluster.Description,
        };

        return StoreInfo(info, serializerOptions, cancellationToken);
    }

    private async Task StoreInfo(ClusterSnapshotInfo info, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        var storageInfoPath = GetStorageInfoPath(info.Id, info.Version);
        if (_fileSystem.File.Exists(storageInfoPath))
            throw new InvalidOperationException($"Cluster info {info.Id}-{info.Version} already exists");

        await using var fileStream = _fileSystem.FileStream.New(storageInfoPath, new FileStreamOptions()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });
        await JsonSerializer.SerializeAsync(fileStream, info, serializerOptions, cancellationToken);
    }
}
