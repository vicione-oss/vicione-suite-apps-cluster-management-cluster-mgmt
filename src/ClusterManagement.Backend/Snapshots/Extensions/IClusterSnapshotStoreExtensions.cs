using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Public;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots.Extensions;

internal static class IClusterSnapshotStoreExtensions
{
    public static async Task<JsonNode?> HandleClusterDependencies(this IClusterSnapshotStore clusterSnapshotStore, Guid clusterId, Version clusterVersion, Action<JsonObject, string, string> dependencyCallback, CancellationToken cancellationToken)
    {
        var cluster = await clusterSnapshotStore.Load(clusterId, clusterVersion, Read, cancellationToken);
        if (cluster is null)
            return null;

        var clusterDependencies = GetClusterDependencyNodes(cluster).ToArray();

        foreach (var clusterDependencyNode in clusterDependencies)
        {
            if (!GetNameAndVersion(clusterDependencyNode, out var name, out var version))
                continue;

            dependencyCallback(clusterDependencyNode, name, version);
        }

        return cluster;

        static IEnumerable<JsonObject> GetClusterDependencyNodes(JsonNode cluster)
            => cluster[nameof(Cluster.Dependencies)]?.AsArray().OfType<JsonObject>() ?? [];
    }

    private static bool GetNameAndVersion(JsonObject clusterDependencyToken, [NotNullWhen(true)] out string? name, [NotNullWhen(true)] out string? version)
    {
        var nameNode = clusterDependencyToken[nameof(ClusterDependency.Name)];
        var versionNode = clusterDependencyToken[nameof(ClusterDependency.Version)];

        if (nameNode is not JsonValue nameToken || versionNode is not JsonValue versionToken)
        {
            name = null;
            version = null;
            return false;
        }

        name = nameToken.GetValue<string>();

        if (SemVersion.TryParse(versionToken.GetValue<string>(), SemVersionStyles.Strict, out _))
        {
            version = versionToken.GetValue<string>();
        }
        else
        {
            version = null;
            return false;
        }


        return true;
    }

    private static async Task<JsonNode?> Read(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<JsonNode>(stream, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);
}
