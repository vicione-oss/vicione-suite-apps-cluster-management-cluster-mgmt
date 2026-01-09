using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Engines.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using MassTransit.Internals;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using Xunit;

namespace ClusterManagement.Backend.Tests.Engines.Consumers;

public class GetEngineLogConsumerFacts : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    protected IClusterSnapshotStore SnapshotStore { get; } = Substitute.For<IClusterSnapshotStore>();

    protected IClusterDbContext DbContext => TestDbContext;
    protected MockFileSystem FileSystem { get; }
    protected Guid ClusterId { get; } = Guid.NewGuid();
    protected Guid EngineId { get; } = Guid.NewGuid();
    protected Guid CommunicationId { get; } = Guid.NewGuid();

    public GetEngineLogConsumerFacts()
    {
        FileSystem = new MockFileSystem();
        var workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
        _ = workspaceService.Cache
            .Returns(FileSystem.Directory.GetCurrentDirectory());

        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetEngineLogConsumer>();
            cfg.AddSingleton(DbContext);
            cfg.AddSingleton(SnapshotStore);
            cfg.AddSingleton<IFileSystem>(FileSystem);
            cfg.AddSingleton(workspaceService);
        };
    }

    public class Consume : GetEngineLogConsumerFacts
    {
        [Fact]
        public async Task Responds_with_error_if_cluster_is_not_active()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);

            // Act
            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.RequestError.Should().NotBeNull();
            response.RequestError!.ErrorCode.Should().Be(ClusterError.ClusterNotActive);
            response.RequestError!.Message.Should().MatchEquivalentOf($"*not*find*cluster*'{ClusterId}'*");
        }

        [Fact]
        public async Task Responds_with_error_if_cluster_could_not_be_found()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            DbContext.SaveActiveClusterVersion(new ViciOne.Cluster.Model.Cluster() { Id = ClusterId, Version = new Version(0, 0, 2) });

            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.RequestError.Should().NotBeNull();
            response.RequestError!.ErrorCode.Should().Be(ClusterError.ClusterIdUnknown);
            response.RequestError!.Message.Should().MatchEquivalentOf($"*not*find*cluster*'{ClusterId}'*");
        }

        [Fact]
        public async Task Responds_with_error_if_engine_could_not_be_found()
        {
            using var builder = ClusterFactory.CreateComplexCluster();
            builder.Cluster.Id = ClusterId;
            DbContext.SaveActiveClusterVersion(builder.Cluster);
            SnapshotStore.SetupLoad(builder.Cluster);

            // Arrange
            await using var tester = new MassTransitTester(_configureServices);

            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.RequestError.Should().NotBeNull();
            response.RequestError!.ErrorCode.Should().Be(ClusterError.EngineUnknown);
            response.RequestError!.Message.Should().MatchEquivalentOf($"*not*find*engine*'{EngineId}'*");
        }

        [Fact]
        public async Task Responds_with_error_if_log_directory_could_not_be_found()
        {
            // Arrange
            using ClusterBuilder builder = new(Substitute.For<IDependencyResolver>());
            builder.Cluster.Id = ClusterId;
            var engine = builder.CreateTopologyToEngine("Engine A", EngineId);
            DbContext.SaveActiveClusterVersion(builder.Cluster);
            SnapshotStore.SetupLoad(builder.Cluster);

            await using var tester = new MassTransitTester(_configureServices);

            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.RequestError.Should().NotBeNull();
            response.RequestError!.ErrorCode.Should().Be(ClusterError.LogUnattainable);
            response.RequestError!.Message.Should().MatchEquivalentOf($"*not*find*directory*engine*log*");
        }

        [Fact]
        public async Task Responds_with_error_if_log_file_could_not_be_found()
        {
            // Arrange
            using ClusterBuilder builder = new(Substitute.For<IDependencyResolver>());
            builder.Cluster.Id = ClusterId;
            var engine = builder.CreateTopologyToEngine("Engine A", EngineId);
            DbContext.SaveActiveClusterVersion(builder.Cluster);
            SnapshotStore.SetupLoad(builder.Cluster);

            FileSystem.AddDirectory("Engines");

            await using var tester = new MassTransitTester(_configureServices);

            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.RequestError.Should().NotBeNull();
            response.RequestError!.ErrorCode.Should().Be(ClusterError.LogUnattainable);
            response.RequestError!.Message.Should().MatchEquivalentOf($"*not*find*log*engine*'{EngineId}'*(Engine A)*");
        }

        [Fact]
        public async Task Responds_with_log()
        {
            // Arrange
            using ClusterBuilder builder = new(Substitute.For<IDependencyResolver>());
            builder.Cluster.Id = ClusterId;
            var engine = builder.CreateTopologyToEngine("Engine A", EngineId);

            DbContext.SaveActiveClusterVersion(builder.Cluster);
            SnapshotStore.SetupLoad(builder.Cluster);

            MockFileData data = new("""
                2024-07-24 16:04:54.123 +01:00 [VRB] Text1
                2024-07-24 16:04:54.12 +01:00 [DBG] Text 2
                2024-07-24 16:04:54.1 +01:00 [INF] Text 3 
                2024-07-24 16:04:54 +00:00 [WRN]  Text 4
                2024-07-24 16:04:57.916 -01:00 [ERR]  Text 5 
                2024-07-24 16:05:00.930 -12:00 [FTL]
                2024-07-24 17:00:00.000 +12:00 [XYZ] Text7
                """);
            FileSystem.AddFile(FileSystem.Path.Combine("Engines", $"{EngineId}.log"), data);

            await using var tester = new MassTransitTester(_configureServices);

            var response = await tester.Harness.TestRequest<GetEngineLogResponse, GetEngineLogRequest>(new GetEngineLogRequest(ClusterId, EngineId));

            // Assert
            response.LogEntries.Should().Equal(
            [
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 15, 4, 54, 123, DateTimeKind.Utc),
                    Type = LogLevel.Trace,
                    Text = "Text1",
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 15, 4, 54, 120, DateTimeKind.Utc),
                    Type = LogLevel.Debug,
                    Text = "Text 2",
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 15, 4, 54, 100),
                    Type = LogLevel.Information,
                    Text = "Text 3 ",
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 16, 4, 54),
                    Type = LogLevel.Warning,
                    Text = " Text 4",
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 17, 4, 57, 916),
                    Type = LogLevel.Error,
                    Text = " Text 5 ",
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 25, 4, 5, 0, 930),
                    Type = LogLevel.Critical,
                },
                new()
                {
                    Timestamp = new DateTime(2024, 7, 24, 5, 0, 0),
                    Type = LogLevel.None,
                    Text = "Text7",
                }
            ]);
            response.RequestError.Should().BeNull();
        }
    }
}

public static class TemporaryTestHarnessExtensions
{
    public static async Task<TResponse> TestRequest<TResponse, TRequest>(
        this ITestHarness harness,
        TRequest request)
        where TResponse : class, IResponse
        where TRequest : class, IInstanceDependentRequest<TResponse>
    {
        await harness.Start();
        await Task.Delay(500);

        var client = harness.Bus.CreateRequestClient<TRequest>();
        var response = await client.GetResponse<TResponse>(request, harness.CancellationToken);

        // did any endpoint consume the message
        Assert.True(await harness.Consumed.Any<TRequest>(harness.CancellationToken));

        await harness.InactivityTask;
        await harness.ThrowOnConsumeFault<TRequest>();

        // if an error occurred it gets swallowed by masstransit - throw it on the tester
        await harness.ThrowOnConsumeError<TRequest>();

        // assert that the response get sent
        Assert.True(await harness.Sent.Any<TResponse>());

        return response.Message;
    }

    private static async Task ThrowOnConsumeError<TMessage>(this ITestHarness harness)
        where TMessage : class
    {
        var consumed = await harness.Consumed.SelectAsync<TMessage>(harness.CancellationToken).ToListAsync();
        var receivedMessage = consumed.FirstOrDefault(k => k.Exception is not null);
        if (receivedMessage is not null)
            throw receivedMessage.Exception;
    }
}
