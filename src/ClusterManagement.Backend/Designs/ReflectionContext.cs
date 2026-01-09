using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace ClusterManagement.Backend.Designs;

internal sealed class ReflectionContext : IDisposable
{
    private readonly MetadataLoadContext _context;

    public ReflectionContext(string mainComponentPath, IEnumerable<string> files, AssemblyLoadContext parentContext)
    {
        var coreAssembly = typeof(int).Assembly;
        _context = new MetadataLoadContext(new DefaultMetaDataAssemblyResolver(mainComponentPath, files, parentContext), coreAssembly.FullName);
    }

    public IReadOnlyList<Assembly> Assemblies
        => [.. _context.GetAssemblies()];

    public void LoadAssemblies(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            using var fileStream = File.OpenRead(path);
            // Is the file really an assembly? https://learn.microsoft.com/en-us/dotnet/standard/assembly/identify#using-the-pereader-class
            using PEReader portableExecutable = new(fileStream);
            if (portableExecutable.HasMetadata)
            {
                var metadata = portableExecutable.GetMetadataReader();
                if (metadata.IsAssembly)
                {
                    fileStream.Position = 0;
                    _context.LoadFromStream(fileStream);
                }
            }
        }
    }

    public void Dispose()
        => _context.Dispose();
}

