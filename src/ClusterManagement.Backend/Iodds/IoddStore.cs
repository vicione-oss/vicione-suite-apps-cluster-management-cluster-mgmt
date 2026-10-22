using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClusterManagement.Backend.Archives;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Iodds.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Iodds;

internal sealed partial class IoddStore(
    ILogger<IoddStore> logger,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    IServiceProvider serviceProvider,
    IOptions<ClusterManagementOptions> options) : IIoddStore
{
    private const string IoddSearchPattern = "*-*-????????-IODD*.xml";

    private readonly ConcurrentDictionary<IoddId, DeviceInfo> _devices = [];
    private readonly Lock _addDeviceLock = new();
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

    public AddDeviceResult AddDevice(ZipArchive archive, out IoddId? ioddId)
    {
        ioddId = null;

        if (!TryCreateArchiveExtractor(archive, out var extractor))
            return AddDeviceResult.ArchiveTooLarge;

        var ioddEntry = archive.Entries.FirstOrDefault(e => IoddFileNameRegex().IsMatch(e.FullName));
        if (ioddEntry is null)
            return AddDeviceResult.NoIoddFile;

        var result = AddDevice(archive.Entries, ioddEntry, extractor, out var id);
        ioddId = id;
        return result;
    }

    public bool TryAddDevices(ZipArchive archive)
    {
        if (!TryCreateArchiveExtractor(archive, out var extractor))
            return false;

        var success = true;
        var byDirectory = archive.Entries
            .GroupBy(e => e.FullName[..(e.FullName.Length - e.Name.Length)]);

        foreach (var group in byDirectory)
        {
            var ioddEntry = group.FirstOrDefault(e => IoddFileNameRegex().IsMatch(e.Name));
            if (ioddEntry is null) continue;

            var result = AddDevice(group, ioddEntry, extractor, out _);
            if (result != AddDeviceResult.Added)
                success = false;

            // The size limit is shared by all devices, so none of the remaining ones can be extracted either.
            if (result == AddDeviceResult.ArchiveTooLarge)
                break;
        }
        return success;
    }

    /// <summary>
    /// Rejects an archive with too many entries or too large declared sizes before any of it is read.
    /// </summary>
    private bool TryCreateArchiveExtractor(ZipArchive archive, [NotNullWhen(true)] out ArchiveExtractor? extractor)
    {
        try
        {
            extractor = ArchiveExtractor.Create(archive, fileSystem, options.Value);
            return true;
        }
        catch (ArchiveTooLargeException ex)
        {
            logger.ArchiveTooLarge(ex);
            extractor = null;
            return false;
        }
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

    public IReadOnlyCollection<int> FillVendorArchive(ZipArchive archive, int vendorId, IEnumerable<int> deviceIds)
    {
        var vendorName = vendorId.ToString(CultureInfo.InvariantCulture);
        var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, vendorName);
        if (!fileSystem.Directory.Exists(vendorDirectory))
            throw new DirectoryNotFoundException($"Vendor directory not found: {vendorDirectory}");

        List<int> archivedDeviceIds = [];

        foreach (var deviceId in deviceIds.Distinct())
        {
            var deviceName = deviceId.ToString(CultureInfo.InvariantCulture);
            var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, deviceName);

            if (!fileSystem.Directory.Exists(deviceDirectory))
            {
                logger.DeviceDirectoryNotFound(vendorId, deviceId);
                continue;
            }

            try
            {
                foreach (var file in fileSystem.Directory.EnumerateFiles(deviceDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    // zip entry paths always use '/' as separator
                    var entryName = $"{vendorName}/{deviceName}/{fileSystem.Path.GetFileName(file)}";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = fileSystem.File.OpenRead(file);
                    fileStream.CopyTo(entryStream);
                }
            }
            catch (Exception ex)
            {
                logger.DeviceArchivingSkipped(vendorId, deviceId, ex);
                continue;
            }

            archivedDeviceIds.Add(deviceId);
        }

        return archivedDeviceIds;
    }

    private AddDeviceResult AddDevice(IEnumerable<ZipArchiveEntry> entries, ZipArchiveEntry ioddEntry, ArchiveExtractor extractor, out IoddId id)
    {
        IODevice device;
        using (var stream = ioddEntry.Open())
            device = IoddParser.ParseStream(stream);

        id = new(device.DeviceIdentity.VendorId, device.DeviceIdentity.DeviceId);

        // Two uploads of the same device would extract into the same directory, and the cleanup of a failed one
        // would delete the files of the other; the later one finds the device instead.
        lock (_addDeviceLock)
        {
            if (_devices.ContainsKey(id))
                return AddDeviceResult.AlreadyExists;

            try
            {
                ExtractDevice(entries, id, extractor);
            }
            catch (ArchiveTooLargeException ex)
            {
                logger.ArchiveTooLarge(ex);
                return AddDeviceResult.ArchiveTooLarge;
            }
            catch (Exception ex)
            {
                logger.DeviceExtractionError(id.VendorId, id.DeviceId, ex);
                return AddDeviceResult.ExtractionFailed;
            }

            // Build DeviceInfo from what we already have — no second disk parse needed
            var fileInfo = fileSystem.FileInfo.New(fileSystem.Path.Combine(GetDeviceDirectory(id), ioddEntry.Name));
            var info = new DeviceInfo
            {
                DeviceDescription = device,
                Filename = ioddEntry.Name,
                LastWriteTime = fileInfo.LastWriteTime.ToUniversalTime(),
            };

            // false means TryAddDevice(IoddId) added it from disk in the meantime
            return _devices.TryAdd(id, info) ? AddDeviceResult.Added : AddDeviceResult.AlreadyExists;
        }
    }

    private void ExtractDevice(IEnumerable<ZipArchiveEntry> entries, IoddId id, ArchiveExtractor extractor)
    {
        fileSystem.Directory.CreateDirectory(_ioddDirectory);
        var vendorDirectory = fileSystem.Path.Combine(_ioddDirectory, id.VendorId.ToString(CultureInfo.InvariantCulture));
        var deviceDirectory = fileSystem.Path.Combine(vendorDirectory, id.DeviceId.ToString(CultureInfo.InvariantCulture));

        // Only what this call created is removed again, an existing directory may belong to another device.
        var createdVendorDirectory = !fileSystem.Directory.Exists(vendorDirectory);
        var createdDeviceDirectory = !fileSystem.Directory.Exists(deviceDirectory);

        List<string> extractedFiles = [];

        try
        {
            fileSystem.Directory.CreateDirectory(vendorDirectory);
            fileSystem.Directory.CreateDirectory(deviceDirectory);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                var filePath = fileSystem.Path.Combine(deviceDirectory, entry.Name);

                if (fileSystem.File.Exists(filePath))
                    continue;

                // Recorded only once extracted: a file that could not be created may belong to someone else,
                // and the extractor removes a file it created but could not finish.
                extractor.ExtractToFile(entry, filePath);
                extractedFiles.Add(filePath);
            }
        }
        catch
        {
            // Don't leave a partial device behind, e.g. the files extracted before the size limit was hit.
            foreach (var filePath in extractedFiles)
                TryDeleteFile(filePath);

            // An empty device directory would make Refresh log a missing IODD file on every run, and keep a device
            // it registered from the partly extracted files in the store until a restart.
            if (createdDeviceDirectory)
                TryDeleteEmptyDirectory(deviceDirectory);

            if (createdVendorDirectory)
                TryDeleteEmptyDirectory(vendorDirectory);

            throw;
        }
    }

    private void TryDeleteFile(string filePath)
    {
        try
        {
            fileSystem.File.Delete(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.PartialFileDeleteError(filePath, ex);
        }
    }

    private void TryDeleteEmptyDirectory(string directory)
    {
        try
        {
            // Not recursive: a directory that still has files, e.g. one that could not be deleted, is kept.
            if (fileSystem.Directory.Exists(directory) && !fileSystem.Directory.EnumerateFileSystemEntries(directory).Any())
                fileSystem.Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.PartialDirectoryDeleteError(directory, ex);
        }
    }

    /// <summary>
    /// Goes through iodds in the directory and adds them to the store if they are not already present.
    /// The directory structure is: <c>&lt;vendor id&gt;/&lt;device id&gt;/&lt;IODD files&gt;</c>
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
