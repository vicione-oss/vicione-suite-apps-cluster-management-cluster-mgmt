using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Public;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

internal class ClusterSnapshotStoreMigration(
    IClusterSnapshotStore clusterSnapshotStore,
    IClusterDependencyStore clusterDependencyStore,
    ILogger<ClusterSnapshotStoreMigration> logger)
{
    public async Task Migrate(CancellationToken cancellationToken)
    {
        try
        {
            var dependencies = await GetDependenciesFromFile(cancellationToken);
            var clusterInfos = await clusterSnapshotStore.GetClusterInfos(null, cancellationToken);

            Stopwatch stopwatch = new();

            foreach (var clusterInfo in clusterInfos)
            {
                logger.LogDebug("Migrate cluster if needed '{ClusterId}' {ClusterVersion}", clusterInfo.Id, clusterInfo.Version);
                stopwatch.Restart();
                await ReplaceDependenciesInCluster(dependencies, clusterInfo, cancellationToken);
                stopwatch.Stop();
                logger.LogDebug("Migrate cluster '{ClusterId}' {ClusterVersion} took {Elapsed}ms", clusterInfo.Id, clusterInfo.Version, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to migrate cluster snapshots.");
        }
    }

    internal async Task<Dictionary<string, ClusterDependency>> GetDependenciesFromFile(CancellationToken cancellationToken)
    {
        var dependencies = await clusterDependencyStore.LoadDependencies(cancellationToken);
        return dependencies.ToDictionary(d => d.Name, StringComparer.Ordinal);
    }

    private async Task ReplaceDependenciesInCluster(Dictionary<string, ClusterDependency> dependencies, Public.Requests.ClusterInfo clusterInfo, CancellationToken cancellationToken)
    {
        var cluster = await clusterSnapshotStore.Load(clusterInfo.Id, clusterInfo.Version, Read, cancellationToken);
        if (cluster is null)
            return;

        var replacedSomething = false;
        var clusterDependencies = GetClusterDependencyNodes(cluster).ToArray();

        foreach (var clusterDependencyNode in clusterDependencies)
        {
            if (!GetNameAndVersion(clusterDependencyNode, out var name, out var version))
                continue;

            if (!dependencies.TryGetValue(name, out var dependency))
                continue;

            if (ReplaceDependencyIfNewer(dependency, version, clusterDependencyNode))
            {
                logger.LogInformation("Replaced dependency '{Name}' with version '{Version}' in cluster '{ClusterId}' {ClusterVersion}.", name, dependency.Version, clusterInfo.Id, clusterInfo.Version);
                replacedSomething = true;
                continue;
            }
        }

        if (replacedSomething)
        {
            var info = new JsonObject
            {
                { nameof(Cluster.Id), clusterInfo.Id },
                { nameof(Cluster.Version), clusterInfo.Version.ToString() },
                { nameof(Cluster.Name), clusterInfo.Name },
                { nameof(Cluster.Description), clusterInfo.Description }
            };
            await clusterSnapshotStore.Store(clusterInfo.Id, clusterInfo.Version, info, cluster, Write, true, cancellationToken);
        }

        static IEnumerable<JsonObject> GetClusterDependencyNodes(JsonNode cluster)
            => cluster[nameof(Cluster.Dependencies)]?.AsArray().OfType<JsonObject>() ?? [];
    }

    private static bool GetNameAndVersion(JsonObject clusterDependencyToken, [NotNullWhen(true)] out string? name, [NotNullWhen(true)] out Version? version)
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

        if (!Version.TryParse(versionToken.GetValue<string>(), out version))
            return false;

        return true;
    }

    private static bool ReplaceDependencyIfNewer(ClusterDependency dependency, Version version, JsonObject clusterDependencyNode)
    {
        if (dependency.Version > version)
        {
            clusterDependencyNode[nameof(ClusterDependency.Version)] = dependency.Version.ToString();
            return true;
        }
        return false;
    }

    private static async Task<JsonNode?> Read(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<JsonNode>(stream, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);

    private static async Task Write<T>(Stream stream, T value, CancellationToken cancellationToken)
        => await JsonSerializer.SerializeAsync(stream, value, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);
}
