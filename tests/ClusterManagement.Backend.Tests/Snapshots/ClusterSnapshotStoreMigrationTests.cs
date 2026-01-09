using System.Text.Json.Nodes;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Snapshots;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.Snapshots;

public class ClusterSnapshotStoreMigration_Migrate
{
    [Fact]
    public async Task Does_nothing_when_no_snapshots_exist()
    {
        var snapshotStore = Substitute.For<IClusterSnapshotStore>();
        var dependencyStore = Substitute.For<IClusterDependencyStore>();
        var logger = Substitute.For<ILogger<ClusterSnapshotStoreMigration>>();
        snapshotStore.GetClusterInfos(default, default).ReturnsForAnyArgs([]);
        ClusterSnapshotStoreMigration migration = new(snapshotStore, dependencyStore, logger);

        var act = FluentActions.Invoking(async () => await migration.Migrate(CancellationToken.None));

        await act.Should().NotThrowAsync();
        await snapshotStore.DidNotReceiveWithAnyArgs().Load<JsonNode?>(default, default!, default!, default);
    }

    [Fact]
    public async Task Migrates_snapshots()
    {
        var snapshotStore = Substitute.For<IClusterSnapshotStore>();
        var dependencyStore = Substitute.For<IClusterDependencyStore>();
        var logger = Substitute.For<ILogger<ClusterSnapshotStoreMigration>>();
        var clusterId = Guid.NewGuid();
        var clusterInfos = new[]
        {
            new Public.Requests.ClusterInfo(clusterId, new(1, 0, 0), "default"),
            new Public.Requests.ClusterInfo(clusterId, new(1, 1, 0), "default"),
        };
        snapshotStore.GetClusterInfos(default, default).ReturnsForAnyArgs(clusterInfos);
        JsonObject clusterOne = new()
        {
            {
                nameof(Cluster.Dependencies),
                new JsonArray
                {
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test1") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.1.0") }
                    },
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test2") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.2.0") }
                    },
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test3") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.2.0") }
                    },
                }
            },
        };
        JsonObject clusterTwo = new()
        {
            {
                nameof(Cluster.Dependencies),
                new JsonArray
                {
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test1") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.2.0") }
                    },
                }
            },
        };
        snapshotStore.Load(clusterId, new(1, 0, 0), Arg.Any<Func<Stream, CancellationToken, Task<JsonNode?>>>(), Arg.Any<CancellationToken>()).Returns(clusterOne);
        snapshotStore.Load(clusterId, new(1, 1, 0), Arg.Any<Func<Stream, CancellationToken, Task<JsonNode?>>>(), Arg.Any<CancellationToken>()).Returns(clusterTwo);
        dependencyStore.LoadDependencies(default)
            .ReturnsForAnyArgs(
            [
                new ClusterDependency { Name = "Test1", Version = new(0, 2, 0), },
                new ClusterDependency { Name = "Test2", Version = new(0, 2, 0), },
                new ClusterDependency { Name = "Test3", Version = new(1, 0, 0), },
            ]);
        ClusterSnapshotStoreMigration migration = new(snapshotStore, dependencyStore, logger);

        await migration.Migrate(CancellationToken.None);

        JsonObject modifiedClusterOne = new()
        {
            {
                nameof(Cluster.Dependencies),
                new JsonArray
                {
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test1") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.2.0") }
                    },
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test2") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("0.2.0") }
                    },
                    new JsonObject
                    {
                        { nameof(ClusterDependency.Name), JsonValue.Create("Test3") },
                        { nameof(ClusterDependency.Version), JsonValue.Create("1.0.0") }
                    },
                }
            },
        };
        JsonObject info = new()
        {
            { nameof(Public.Requests.ClusterInfo.Id), JsonValue.Create(clusterId) },
            { nameof(Public.Requests.ClusterInfo.Version), JsonValue.Create("1.0.0") },
            { nameof(Public.Requests.ClusterInfo.Name), JsonValue.Create("default") },
            { nameof(Public.Requests.ClusterInfo.Description), null },
        };
        await snapshotStore.Received().Store(clusterId, new(1, 0, 0), Arg.Is<JsonObject>(e => JsonNode.DeepEquals(e, info)), Arg.Is<JsonNode>(e => JsonNode.DeepEquals(e, modifiedClusterOne)), Arg.Any<Func<Stream, JsonNode, CancellationToken, Task>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await snapshotStore.DidNotReceive().Store(clusterId, new(1, 1, 0), Arg.Any<JsonObject>(), clusterTwo, Arg.Any<Func<Stream, JsonNode, CancellationToken, Task>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}

public class ClusterSnapshotStoreMigration_GetDependenciesFromFile
{
    [Fact]
    public async Task Returns_dependencies_from_file()
    {
        var snapshotStore = Substitute.For<IClusterSnapshotStore>();
        var dependencyStore = Substitute.For<IClusterDependencyStore>();
        var logger = Substitute.For<ILogger<ClusterSnapshotStoreMigration>>();
        var functionBlockDependencies = new[]
        {
            new ClusterDependency { Name = "Test1", Version = new(0, 1, 0), },
            new ClusterDependency { Name = "Test2", Version = new(0, 2, 0), },
            new ClusterDependency { Name = "Test3", Version = new(1, 0, 0), },
        };
        dependencyStore.LoadDependencies(default).ReturnsForAnyArgs(functionBlockDependencies);
        ClusterSnapshotStoreMigration migration = new(snapshotStore, dependencyStore, logger);

        var result = await migration.GetDependenciesFromFile(CancellationToken.None);

        result.Should().BeEquivalentTo(
            new Dictionary<string, ClusterDependency>
            {
                ["Test1"] = functionBlockDependencies[0],
                ["Test2"] = functionBlockDependencies[1],
                ["Test3"] = functionBlockDependencies[2],
            });
    }

    [Fact]
    public async Task Throws_if_dependency_exists_in_different_versions()
    {
        var snapshotStore = Substitute.For<IClusterSnapshotStore>();
        var dependencyStore = Substitute.For<IClusterDependencyStore>();
        var logger = Substitute.For<ILogger<ClusterSnapshotStoreMigration>>();
        var functionBlockDependencies = new[]
        {
            new ClusterDependency { Name = "Test1", Version = new(0, 1, 0), },
            new ClusterDependency { Name = "Test2", Version = new(0, 2, 0), },
            new ClusterDependency { Name = "Test2", Version = new(0, 1, 0), },
            new ClusterDependency { Name = "Test3", Version = new(1, 0, 0), },
        };
        dependencyStore.LoadDependencies(default).ReturnsForAnyArgs(functionBlockDependencies);
        ClusterSnapshotStoreMigration migration = new(snapshotStore, dependencyStore, logger);

        var act = FluentActions.Invoking(async () => await migration.GetDependenciesFromFile(CancellationToken.None));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*same*key*");
    }
}
