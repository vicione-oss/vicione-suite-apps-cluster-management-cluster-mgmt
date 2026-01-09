using System.IO.Compression;
using System.Net.Mime;
using ClusterManagement.Public;
using Microsoft.AspNetCore.Components.Forms;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class IBrowseFileExtensions
{
    private const string UnregisteredZipType = "application/x-zip-compressed";

    public static async Task<Cluster> DeserializeCluster(this IBrowserFile browserFile, long maxClusterSize)
    {
        if (browserFile.ContentType is not (MediaTypeNames.Application.Json or MediaTypeNames.Application.Zip or UnregisteredZipType))
            throw new InvalidOperationException("Cluster upload is only supported for file type 'json|zip'.");

        await using var stream = browserFile.OpenReadStream(maxClusterSize);

        if (browserFile.ContentType == MediaTypeNames.Application.Json)
        {
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return ClusterSerializer.Deserialize(json);
        }

        {
            // Synchronous reads are not supported by ZipArchive so we need to use a memory stream
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;// rewind

            // we expect a single json within the zip
            using var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read);
            var jsonStream = zipArchive.Entries.First().Open();
            using var reader = new StreamReader(jsonStream);
            var json = await reader.ReadToEndAsync();
            return ClusterSerializer.Deserialize(json);
        }
    }
}
