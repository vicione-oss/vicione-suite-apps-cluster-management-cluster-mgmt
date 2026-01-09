using System.Reflection;
using System.Runtime.Loader;

namespace ClusterManagement.Backend.Designs;

public class DefaultMetaDataAssemblyResolver(string mainComponentPath, IEnumerable<string> paths, AssemblyLoadContext parentContext) : MetadataAssemblyResolver
{
    private readonly AssemblyDependencyResolver _resolver = new(mainComponentPath);
    private readonly Dictionary<string, string> _paths = paths.ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => p, StringComparer.OrdinalIgnoreCase);

    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        _paths.TryGetValue(assemblyName.Name ?? string.Empty, out var path);
        path ??= ResolveAssemblyToPath(assemblyName);
        path ??= parentContext.LoadFromAssemblyName(assemblyName)?.Location;
        path ??= Assembly.Load(assemblyName).Location;

        if (string.IsNullOrEmpty(path))
            return null;
        return LoadWithStreamFromPath(context, path);

        string? ResolveAssemblyToPath(AssemblyName asseName)
        {
            var thePath = _resolver.ResolveAssemblyToPath(asseName);
            return !string.IsNullOrEmpty(thePath) ? thePath : null;
        }
    }

    internal static Assembly LoadWithStreamFromPath(MetadataLoadContext context, string path)
    {
        using var file = File.OpenRead(path);
        return context.LoadFromStream(file);
    }
}
