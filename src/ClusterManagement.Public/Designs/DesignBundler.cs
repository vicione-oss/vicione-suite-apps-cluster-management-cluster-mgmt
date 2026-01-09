using System.IO.Compression;

namespace ClusterManagement.Public.Designs;

public static class DesignBundler
{
    public static async Task<byte[]> CreateArchive(Dictionary<string, string> files)
    {
        // create zip file containing assemblies and debug symbols
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                await using var fileStream = new FileStream(file.Value, FileMode.Open, FileAccess.Read);
                await using var entryStream = archive.CreateEntry(file.Key).Open();

                await fileStream.CopyToAsync(entryStream);
            }
        }

        return memoryStream.ToArray();
    }

    public static async Task<DesignAssembliesRaw> ExtractArchive(byte[] pluginZip, List<string?> excludeAssemblies)
    {
        using var archive = new ZipArchive(new MemoryStream(pluginZip));
        var dlls = new Dictionary<string, byte[]>();
        var pdbs = new Dictionary<string, byte[]>();
        var excluded = new List<string>();

        foreach (var entry in archive.Entries)
        {
            // add only assemblies that are not in current domain
            if (excludeAssemblies.Contains(Path.GetFileNameWithoutExtension(entry.FullName)))
            {
                excluded.Add(entry.FullName);
                continue;
            }

            using var memoryStream = new MemoryStream();
            await entry.Open().CopyToAsync(memoryStream);
            var file = memoryStream.ToArray();
            switch (Path.GetExtension(entry.FullName).ToUpperInvariant())
            {
                case ".DLL":
                    dlls.Add(entry.FullName, file);
                    break;
                case ".PDB":
                    pdbs.Add(entry.FullName, file);
                    break;
            }
        }

        return new DesignAssembliesRaw(dlls, pdbs, excluded);
    }
}
