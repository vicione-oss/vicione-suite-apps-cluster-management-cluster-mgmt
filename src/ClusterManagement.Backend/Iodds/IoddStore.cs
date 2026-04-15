using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Iodds.Extensions;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Iodds;

internal sealed partial class IoddStore(ILogger<IoddStore> logger, IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider, IFileSystem fileSystem, IServiceProvider serviceProvider) : IIoddStore
{
    private const string IoddSearchPattern = "*-*-????????-IODD*.xml";

    private readonly ConcurrentDictionary<IoddId, DeviceInfo> _devices = [];
    private readonly string _ioddDirectory = fileSystem.GetIoddsPath(workspaceProvider);

    public string IoddDirectory => _ioddDirectory;

    public IReadOnlyCollection<DeviceInfo> Devices => [.. _devices.Values];

    public bool GetAutoDownloadIodds()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IClusterEditorDbContext>();
        var existingSettings = dbContext.IoddSettings.FirstOrDefault(s => s.Id == 1);
        return existingSettings?.AutoDownload ?? true;
    }

    public async Task SetAutoDownloadIodds(bool value, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IClusterEditorDbContext>();
        var settings = dbContext.IoddSettings.FirstOrDefault(s => s.Id == 1);

        if (settings is null)
        {
            settings = new()
            {
                AutoDownload = value,
            };
            dbContext.IoddSettings.Add(settings);
        }
        else
        {
            settings.AutoDownload = value;
            dbContext.IoddSettings.Update(settings);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public bool TryAddDevice(IoddId id)
    {
        if (_devices.ContainsKey(id))
            return false;

        if (!TryReadDevice(id, GetDeviceDirectory(id), out var device))
            return false;

        return _devices.TryAdd(id, device);  // sole authority — false means a concurrent add won
    }

    public bool TryAddDevice(ZipArchive archive, [NotNullWhen(true)] out IoddId? ioddId)
    {
        ioddId = null;
        var ioddEntry = archive.Entries.FirstOrDefault(e => IoddFileNameRegex().IsMatch(e.FullName));
        if (ioddEntry is null)
            return false;

        return TryAddDevice(archive.Entries, ioddEntry, out ioddId);
    }

    public bool TryAddDevices(ZipArchive archive)
    {
        var success = true;
        var byDirectory = archive.Entries
            .GroupBy(e => e.FullName[..(e.FullName.Length - e.Name.Length)]);

        foreach (var group in byDirectory)
        {
            var ioddEntry = group.FirstOrDefault(e => IoddFileNameRegex().IsMatch(e.Name));
            if (ioddEntry is null) continue;
            if (!TryAddDevice(group, ioddEntry, out _))
                success = false;
        }
        return success;
    }

    public bool TryRemoveDevice(IoddId id)
    {
        if (!_devices.TryGetValue(id, out _))
            return false;

        try
        {
            var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
            var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));

            if (fileSystem.Directory.Exists(deviceDirectory))
                fileSystem.Directory.Delete(deviceDirectory, true);

            if (!fileSystem.Directory.EnumerateDirectories(vendorDirectory).Any())
                fileSystem.Directory.Delete(vendorDirectory, true);
        }
        catch { return false; }

        _devices.TryRemove(id, out _);
        return true;
    }

    public bool TryRemoveDevices(IEnumerable<IoddId> ids)
    {
        var success = true;
        foreach (var id in ids)
        {
            if (!TryRemoveDevice(id))
                success = false;
        }
        return success;
    }

    public void FillDeviceArchive(ZipArchive archive, IoddId id)
    {
        var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
        if (!fileSystem.Directory.Exists(vendorDirectory))
            throw new DirectoryNotFoundException($"Vendor directory not found: {vendorDirectory}");

        var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));
        if (!fileSystem.Directory.Exists(deviceDirectory))
            throw new DirectoryNotFoundException($"Device directory not found: {deviceDirectory}");

        foreach (var file in fileSystem.Directory.EnumerateFiles(deviceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var entry = archive.CreateEntry(fileSystem.Path.GetFileName(file), CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var fileStream = fileSystem.File.OpenRead(file);
            fileStream.CopyTo(entryStream);
        }
    }

    private bool TryAddDevice(IEnumerable<ZipArchiveEntry> entries, ZipArchiveEntry ioddEntry, out IoddId id)
    {
        IODevice device;
        using (var stream = ioddEntry.Open())
            device = IoddParser.ParseStream(stream);

        id = new(device.DeviceIdentity.VendorId, device.DeviceIdentity.DeviceId);

        if (_devices.ContainsKey(id))
            return false;

        try { ExtractDevice(entries, id); }
        catch (Exception ex) { logger.DeviceExtractionError(id.VendorId, id.DeviceId, ex); return false; }

        // Build DeviceInfo from what we already have — no second disk parse needed
        var fileInfo = fileSystem.FileInfo.New(fileSystem.Path.Combine(GetDeviceDirectory(id), ioddEntry.Name));
        var info = new DeviceInfo
        {
            DeviceDescription = device,
            Filename = ioddEntry.Name,
            LastWriteTime = fileInfo.LastWriteTime.ToUniversalTime(),
        };

        return _devices.TryAdd(id, info);
    }

    private void ExtractDevice(IEnumerable<ZipArchiveEntry> entries, IoddId id)
    {
        fileSystem.Directory.CreateDirectory(_ioddDirectory);
        var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
        var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));

        fileSystem.Directory.CreateDirectory(vendorDirectory);
        fileSystem.Directory.CreateDirectory(deviceDirectory);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var filePath = fileSystem.Path.Combine(deviceDirectory, entry.Name);

            if (fileSystem.File.Exists(filePath))
                continue;

            using var fileStream = fileSystem.File.Create(filePath);
            using var entryStream = entry.Open();
            entryStream.CopyTo(fileStream);
        }
    }

    /// <summary>
    /// Goes through iodds in the directory and adds them to the store if they are not already present.
    /// The directory structure is: <vendor id>/<device id>/<IODD files>
    /// </summary>
    public void Refresh()
    {
        try
        {
            fileSystem.Directory.CreateDirectory(_ioddDirectory);
        }
        catch (Exception ex)
        {
            logger.LogDirectoryCreationError(ex, _ioddDirectory);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var deviceCount = 0;
        ConcurrentDictionary<IoddId, byte> existingDevices = [];

        var allDeviceDirectories = fileSystem.Directory
            .EnumerateDirectories(_ioddDirectory)
            .SelectMany(vendorDirectory =>
            {
                var vendorDirectoryName = fileSystem.Path.GetFileName(vendorDirectory);
                if (!int.TryParse(vendorDirectoryName, CultureInfo.InvariantCulture, out var vendorId))
                {
                    logger.InvalidVendorDirectoryName(vendorDirectoryName);
                    return [];
                }
                return fileSystem.Directory.EnumerateDirectories(vendorDirectory)
                    .Select(d => (VendorId: vendorId, DeviceDirectory: d));
            });

        Parallel.ForEach(allDeviceDirectories, item =>
        {
            var deviceDirectoryName = fileSystem.Path.GetFileName(item.DeviceDirectory);

            if (!int.TryParse(deviceDirectoryName, CultureInfo.InvariantCulture, out var deviceId))
            {
                logger.InvalidDeviceDirectoryName(deviceDirectoryName);
                return;
            }

            var id = new IoddId(item.VendorId, deviceId);
            existingDevices.TryAdd(id, 0);

            if (TryAddDevice(id))
                Interlocked.Increment(ref deviceCount);
        });

        stopwatch.Stop();
        logger.AddedDevicesToStore(deviceCount, stopwatch.ElapsedMilliseconds);

        // Thread-safe cleanup of obsolete devices
        var devicesToRemove = _devices.Keys.Except(existingDevices.Keys).ToArray();
        foreach (var id in devicesToRemove)
        {
            _devices.TryRemove(id, out _);
        }
    }

    private bool TryReadDevice(IoddId id, string deviceDirectory, [NotNullWhen(true)] out DeviceInfo? device)
    {
        IFileInfo? fileInfo = null;

        foreach (var filename in fileSystem.Directory.EnumerateFiles(deviceDirectory, IoddSearchPattern))
        {
            var currentFileInfo = fileSystem.FileInfo.New(filename);

            if (IoddFileNameRegex().IsMatch(currentFileInfo.Name))
            {
                fileInfo = currentFileInfo;
                break;
            }
        }

        if (fileInfo is null)
        {
            logger.IoddFileNotFound(id.VendorId, id.DeviceId);
            device = null;
            return false;
        }

        try
        {
            device = new()
            {
                DeviceDescription = IoddParser.ParseFile(fileInfo.FullName, fileSystem),
                Filename = fileInfo.Name,
                LastWriteTime = fileInfo.LastWriteTime.ToUniversalTime(),
            };
        }
        catch (Exception ex)
        {
            logger.IoddFileParseError(fileInfo.FullName, ex);
            device = null;
            return false;
        }

        return true;
    }

    private string GetDeviceDirectory(IoddId id)
        => fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture), id.DeviceId.ToString(CultureInfo.InvariantCulture));

    // <vendor name>-<device name>-<release date>-IODD<schema version>.xml
    [GeneratedRegex("[A-Za-z0-9_#-]+-[A-Za-z0-9_#-]+-\\d+-IODD\\d\\.\\d(?:\\.\\d+)?\\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IoddFileNameRegex();
}
