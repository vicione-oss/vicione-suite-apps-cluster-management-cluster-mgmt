using System.IO.Compression;
using ClusterManagement.Backend.Designs.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Tests.Tools.Factories;

public static class PackageFactory
{
    public static readonly ClusterDependency ConstantPackageDependency = new()
    {
        Name = "ViciOne.Constant",
        Version = "0.34.0"
    };

    public static readonly ClusterDependency LogicPackageDependency = new()
    {
        Name = "ViciOne.Logic",
        Version = "0.34.0"
    };

    public static byte[] CreatePackageZipArchiveBytes(string packageName, string version, string? depsJsonContent = null)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add package.json or manifest
            var manifestEntry = archive.CreateEntry($"{packageName}.deps.json", CompressionLevel.Fastest);
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                if (depsJsonContent is not null)
                {
                    writer.Write(depsJsonContent);
                }
                else
                {
                    writer.Write($"\"{packageName}/{version}\": {{");
                }
            }

            // Add other required files
            var dllEntry = archive.CreateEntry($"{packageName}.dll", CompressionLevel.Fastest);
            using (var entryStream = dllEntry.Open())
            {
                entryStream.Write([0x4D, 0x5A]); // PE header magic
            }
        }

        return memoryStream.ToArray();
    }

    public static byte[] CreateInvalidPackageZipArchiveBytes(string packageName)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add other required files
            var dllEntry = archive.CreateEntry($"{packageName}.dll", CompressionLevel.Fastest);
            using var entryStream = dllEntry.Open();
            entryStream.Write([0x4D, 0x5A]); // PE header magic

        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Return a byte array representing a ZIP archive of the specified package dependency handled in memory
    /// </summary>    
    public static byte[] CreatePackageArchiveFromDependency(string packageName)
    {
        var dependency = GetDependencyByName(packageName);
        var executionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var dependencyFolder = Path.Combine(executionPath!, IFileSystemExtensions.DependenciesDirectory, dependency.Name, dependency.Version.ToString());

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var filePath in Directory.EnumerateFiles(dependencyFolder, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(dependencyFolder, filePath);
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(entryStream);
            }
        }

        return memoryStream.ToArray();
    }

    private static ClusterDependency GetDependencyByName(string packageName)
    {
        if (packageName == ConstantPackageDependency.Name)
            return ConstantPackageDependency;

        if (packageName == LogicPackageDependency.Name)
            return LogicPackageDependency;

        throw new InvalidOperationException($"This method only supports creating packages for '{ConstantPackageDependency.Name}' and '{LogicPackageDependency.Name}'.");
    }
}
