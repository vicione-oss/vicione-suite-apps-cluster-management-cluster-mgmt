using System.IO.Compression;

namespace ClusterManagement.Tests.Tools.Factories;

public static class PackageFactory
{
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
}
