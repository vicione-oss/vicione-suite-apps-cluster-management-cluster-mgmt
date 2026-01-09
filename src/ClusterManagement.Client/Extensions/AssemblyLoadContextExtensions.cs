using System.Runtime.Loader;

namespace ClusterManagement.Client.Extensions;

public static class AssemblyLoadContextExtensions
{
    public static List<string?> GetContextAssemblyNames(this AssemblyLoadContext context)
        => [.. context.Assemblies
            .Select(a => a.GetName().Name)
            .OrderBy(k => k)];
}
