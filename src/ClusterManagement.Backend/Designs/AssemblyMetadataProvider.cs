using System.IO.Abstractions;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ClusterManagement.Backend.Designs;

internal class AssemblyMetadataProvider
{
    public static (string? TargetFramework, string? RuntimeVersion, Machine PlatformTarget) GetAssemblyInfo(IFileSystem fileSystem, string assemblyPath)
    {
        using var stream = fileSystem.File.OpenRead(assemblyPath);

        return GetAssemblyInfo(stream);
    }

    public static (string? TargetFramework, string? RuntimeVersion, Machine PlatformTarget) GetAssemblyInfo(Stream assemblyStream)
    {
        using var peReader = new PEReader(assemblyStream);

        // Get platform target (x86, x64, AnyCPU, ARM, etc.)
        var platformTarget = peReader.PEHeaders.CoffHeader.Machine;

        // Get metadata reader
        var metadataReader = peReader.GetMetadataReader();

        string? targetFramework = null;
        string? runtimeVersion = null;

        // Read custom attributes to find TargetFrameworkAttribute
        foreach (var attributeHandle in metadataReader.CustomAttributes)
        {
            var attribute = metadataReader.GetCustomAttribute(attributeHandle);
            var attributeType = GetAttributeTypeName(metadataReader, attribute);

            if (attributeType == "System.Runtime.Versioning.TargetFrameworkAttribute")
            {
                targetFramework = GetAttributeValue(metadataReader, attribute);
            }
        }

        // Get runtime version from metadata
        runtimeVersion = metadataReader.MetadataVersion;

        return (targetFramework, runtimeVersion, platformTarget);
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind == HandleKind.MemberReference)
        {
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
            return $"{reader.GetString(typeRef.Namespace)}.{reader.GetString(typeRef.Name)}";
        }
        return string.Empty;
    }

    private static string? GetAttributeValue(MetadataReader reader, CustomAttribute attribute)
    {
        var blobReader = reader.GetBlobReader(attribute.Value);
        blobReader.ReadUInt16(); // Prolog
        return blobReader.ReadSerializedString();
    }
}
