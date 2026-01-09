using System.Reflection;
using ClusterManagement.Public.Designs;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Extensions;

public static class IDesignProviderExtensions
{
    public static async Task<ClusterDependency> FindClusterDependency(this IDesignProvider designProvider, string packageName, Version? version = null)
        => (await FindClusterDependencyOrDefault(designProvider, packageName, version))
            ?? throw new InvalidOperationException($"No package {packageName} {version} found");

    public static async Task<ClusterDependency?> FindClusterDependencyOrDefault(this IDesignProvider designProvider, string packageName, Version? version = null)
        => (await designProvider.GetClusterDependencies()).FirstOrDefault(k => k.Name == packageName && (version is null || Equals(k.Version, version)));

    public static Task<ClusterDependency> FindClusterDependency(this IDesignProvider designProvider, Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        if (string.IsNullOrEmpty(assemblyName.Name))
            throw new InvalidOperationException("Failed to get name of assembly");

        return designProvider.FindClusterDependency(assemblyName.Name);
    }
}
