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

    public bool AutoDownloadIodds
    {
        get
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IClusterEditorDbContext>();
            var existingSettings = dbContext.IoddSettings.FirstOrDefault(s => s.Id == 1);
            return existingSettings?.AutoDownload ?? true;
        }
        set
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

            dbContext.Instance.SaveChangesAsync();
        }
    }

    public string IoddDirectory => _ioddDirectory;
    public IReadOnlyCollection<DeviceInfo> Devices => [.. _devices.Values];

    public bool TryAddDevice(IoddId id)
    {
        fileSystem.Directory.CreateDirectory(_ioddDirectory);

        var deviceDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture), id.DeviceId.ToString(CultureInfo.InvariantCulture));

        if (_devices.ContainsKey(id))
            return false;
        if (!TryReadDevice(id, deviceDirectory, out var device))
            return false;
        if (!_devices.TryAdd(id, device))
            logger.DeviceAlreadyExists(id.VendorId, id.DeviceId);
        return true;
    }

    public bool TryAddDevice(ZipArchive archive, out IoddId ioddId)
    {
        var ioddEntry = archive.Entries.Where(e => IoddFileNameRegex().IsMatch(e.FullName)).FirstOrDefault()
            ?? throw new InvalidDataException("No IODD file found in the archive.");

        return TryAddDevice(archive.Entries, ioddEntry, out ioddId);
    }

    public bool TryAddDevices(ZipArchive archive)
    {
        var ioddEntries = archive.Entries.Where(e => IoddFileNameRegex().IsMatch(e.FullName));
        var success = true;

        foreach (var ioddEntry in ioddEntries)
        {
            var directory = ioddEntry.FullName.Replace(ioddEntry.Name, string.Empty, StringComparison.InvariantCulture);

            if (!TryAddDevice(archive.Entries.Where(e => e.FullName.StartsWith(directory, StringComparison.InvariantCulture)), ioddEntry, out _))
                success = false;
        }
        return success;
    }

    public bool TryRemoveDevice(IoddId id)
    {
        if (_devices.TryRemove(id, out _))
        {
            try
            {
                var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
                var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));

                if (fileSystem.Directory.Exists(deviceDirectory))
                    fileSystem.Directory.Delete(deviceDirectory, true);

                if (!fileSystem.Directory.EnumerateDirectories(vendorDirectory).Any())
                    fileSystem.Directory.Delete(vendorDirectory, true);

                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
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
        using (var stream = ioddEntry.Open())
        {
            var device = IoddParser.ParseStream(stream);
            id = new(device.DeviceIdentity.VendorId, device.DeviceIdentity.DeviceId);
        }

        if (_devices.ContainsKey(id))
            return false;

        try
        {
            ExtractDevice(entries, id);
        }
        catch (Exception ex)
        {
            logger.DeviceExtractionError(id.VendorId, id.DeviceId, ex);
            return false;
        }

        if (TryAddDevice(id))
            return true;
        return false;
    }

    private void ExtractDevice(IEnumerable<ZipArchiveEntry> entries, IoddId id)
    {
        fileSystem.Directory.CreateDirectory(_ioddDirectory);
        var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
        var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));

        if (!fileSystem.Directory.Exists(vendorDirectory))
            fileSystem.Directory.CreateDirectory(vendorDirectory);
        if (!fileSystem.Directory.Exists(deviceDirectory))
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
        fileSystem.Directory.CreateDirectory(_ioddDirectory);

        var stopwatch = Stopwatch.StartNew();
        var deviceCount = 0;
        ConcurrentBag<IoddId> existingDevices = [];

        foreach (var vendorDirectory in fileSystem.Directory.EnumerateDirectories(_ioddDirectory))
        {
            var vendorDirectoryName = fileSystem.Path.GetFileName(vendorDirectory);

            if (!int.TryParse(vendorDirectoryName, CultureInfo.InvariantCulture, out var vendorId))
            {
                logger.InvalidVendorDirectoryName(vendorDirectoryName);
                continue;
            }

            Parallel.ForEach(fileSystem.Directory.EnumerateDirectories(vendorDirectory), (deviceDirectory, _, _) =>
            {
                var deviceDirectoryName = fileSystem.Path.GetFileName(deviceDirectory);

                if (!int.TryParse(deviceDirectoryName, CultureInfo.InvariantCulture, out var deviceId))
                {
                    logger.InvalidDeviceDirectoryName(deviceDirectoryName);
                    return;
                }

                existingDevices.Add(new IoddId(vendorId, deviceId));

                if (TryAddDevice(new IoddId(vendorId, deviceId)))
                    Interlocked.Increment(ref deviceCount);
            });
        }

        stopwatch.Stop();
        logger.AddedDevicesToStore(deviceCount, stopwatch.ElapsedMilliseconds);

        Parallel.ForEach(_devices.Keys.Except(existingDevices).ToArray(), (id, _, _) => _devices.TryRemove(id, out _));
    }

    private bool TryReadDevice(IoddId id, string deviceDirectory, [NotNullWhen(true)] out DeviceInfo? device)
    {
        FileInfo? fileInfo = null;

        foreach (var filename in fileSystem.Directory.EnumerateFiles(deviceDirectory, IoddSearchPattern))
        {
            FileInfo currentFileInfo = new(filename);

            if (IoddFileNameRegex().IsMatch(currentFileInfo.Name))
            {
                fileInfo = currentFileInfo;
                break;
            }
        }

        if (fileInfo is null)
        {
            logger.IoddFileNotFound(id.VendorId, id.VendorId);
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

    // <vendor name>-<device name>-<release date>-IODD<schema version>.xml
    [GeneratedRegex("[A-Za-z0-9_#-]+-[A-Za-z0-9_#-]+-\\d+-IODD\\d\\.\\d(?:\\.\\d+)?\\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IoddFileNameRegex();
}
