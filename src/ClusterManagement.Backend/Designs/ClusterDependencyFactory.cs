using System.Reflection;
using ClusterManagement.Backend.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal static class ClusterDependencyFactory
{
    public static ClusterDependency CreateDependency(Type type)
        => CreateDependency(type.Assembly);

    public static ClusterDependency CreateDependency(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var assemblyVersion = assemblyName.Version ?? throw new InvalidDataException();

        // assembly version might be 0.1.0.0 but our version has major.minor.build
        return new()
        {
            Name = assemblyName.Name!,
            Version = assemblyVersion.ToVersionString(),
        };
    }
}
