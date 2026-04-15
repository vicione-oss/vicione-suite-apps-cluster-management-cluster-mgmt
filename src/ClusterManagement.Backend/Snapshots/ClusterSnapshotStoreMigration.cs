using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Public;
using Microsoft.Extensions.Logging;
using Semver;
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
                logger.LogMigrationStart(clusterInfo.Id, clusterInfo.Version);
                stopwatch.Restart();
                await ReplaceDependenciesInCluster(dependencies, clusterInfo, cancellationToken);
                stopwatch.Stop();
                logger.LogMigrationEnd(clusterInfo.Id, clusterInfo.Version, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogMigrationError(ex);
        }
    }

    internal async Task<Dictionary<string, ClusterDependency>> GetDependenciesFromFile(CancellationToken cancellationToken)
    {
        var dependencies = await clusterDependencyStore.LoadDependencies(cancellationToken);
        return dependencies.ToDictionary(d => d.Name, StringComparer.Ordinal);
    }

    private async Task ReplaceDependenciesInCluster(Dictionary<string, ClusterDependency> dependencies, Public.Requests.ClusterInfo clusterInfo, CancellationToken cancellationToken)
    {
        var replacedSomething = false;

        var cluster = await clusterSnapshotStore.HandleClusterDependencies(clusterInfo.Id, clusterInfo.Version, async (dependencyNode, name, version) =>
        {
            if (!dependencies.TryGetValue(name, out var dependency))
                return;

            if (ReplaceDependencyIfNewer(dependency, version, dependencyNode))
            {
                logger.LogDependencyReplaced(name, dependency.Version, clusterInfo.Id, clusterInfo.Version);
                replacedSomething = true;
            }
        }, cancellationToken);

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
    }

    private static bool ReplaceDependencyIfNewer(ClusterDependency dependency, string version, JsonObject clusterDependencyNode)
    {
        var dependencyVersion = SemVersion.Parse(dependency.Version, SemVersionStyles.Strict);
        var otherVersion = SemVersion.Parse(version, SemVersionStyles.Strict);

        if (dependencyVersion.CompareSortOrderTo(otherVersion) == 1)
        {
            clusterDependencyNode[nameof(ClusterDependency.Version)] = dependency.Version.ToString();
            return true;
        }
        return false;
    }

    private static async Task Write<T>(Stream stream, T value, CancellationToken cancellationToken)
        => await JsonSerializer.SerializeAsync(stream, value, ClusterSerializer.TypedSerializerOptions ?? ClusterSerializer.UntypedSerializerOptions, cancellationToken);
}
