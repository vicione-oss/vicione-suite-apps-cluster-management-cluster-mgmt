using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class EngineCycleCollectorTests
{
    private readonly IClusterService _clusters = Substitute.For<IClusterService>();
    private readonly IEngineHostBrokerProvider _brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IMqttClient _client = Substitute.For<IMqttClient>();
    private readonly Guid _instance = Guid.NewGuid();

    public EngineCycleCollectorTests()
    {
        _client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MqttClientConnectResult())
            .AndDoes(_ => _client.IsConnected.Returns(true));
        _brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, EngineHostBroker>
            {
                [_instance] = Broker(),
            });
    }

    [Fact]
    public async Task Start_watches_the_engine_hosts_of_the_running_version()
    {
        // Arrange
        var host = Guid.NewGuid();
        RunningVersionHas(host);
        await using var sut = CreateCollector(out _);

        // Act
        sut.Start();

        // Assert
        await WaitUntil(() => SubscribedTopics().Contains(EngineCycleReport.InfoTopic(host)));
        SubscribedTopics().Should().Equal(EngineCycleReport.InfoTopic(host));
    }

    [Fact]
    public async Task Start_connects_no_broker_without_a_running_version()
    {
        // Arrange
        _clusters.QueryClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns([new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 0), "Draft")]);
        await using var sut = CreateCollector(out _);

        // Act
        sut.Start();

        // Assert
        await WaitUntil(() => _clusters.ReceivedCalls().Any());
        await _client.DidNotReceive().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_completed_deployment_moves_the_watch_to_the_new_running_version()
    {
        // Arrange
        var previousHost = Guid.NewGuid();
        var newHost = Guid.NewGuid();
        RunningVersionHas(previousHost);
        await using var sut = CreateCollector(out _);
        sut.Start();
        await WaitUntil(() => SubscribedTopics().Contains(EngineCycleReport.InfoTopic(previousHost)));
        RunningVersionHas(newHost);

        // Act
        await sut.Consume(new ClientContext<ClusterUpdateCompleted>(
            new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0), TimeSpan.FromSeconds(1)), null),
            TestContext.Current.CancellationToken);

        // Assert
        await WaitUntil(() => SubscribedTopics().Contains(EngineCycleReport.InfoTopic(newHost)));
        await _client.Received(1).UnsubscribeAsync(
            Arg.Is<MqttClientUnsubscribeOptions>(o => o.TopicFilters.Single() == EngineCycleReport.InfoTopic(previousHost)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_load_is_retried()
    {
        // Arrange
        var host = Guid.NewGuid();
        var cluster = RunningVersionHas(host);
        _clusters.QueryClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new TimeoutException(),
                _ => [new ClusterInfo(cluster.Id, cluster.Version, cluster.Name, IsActive: true)]);
        await using var sut = CreateCollector(out _, new ImmediateTimeProvider());

        // Act
        sut.Start();

        // Assert
        await WaitUntil(() => SubscribedTopics().Contains(EngineCycleReport.InfoTopic(host)));
        SubscribedTopics().Should().Equal(EngineCycleReport.InfoTopic(host));
    }

    [Fact]
    public async Task A_failed_broker_lookup_is_retried()
    {
        // Arrange
        var host = Guid.NewGuid();
        RunningVersionHas(host);
        var broker = Broker();
        _brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<Guid, EngineHostBroker>>(
                _ => throw new TimeoutException(),
                _ => new Dictionary<Guid, EngineHostBroker> { [_instance] = broker });
        await using var sut = CreateCollector(out _, new ImmediateTimeProvider());

        // Act
        sut.Start();

        // Assert
        await WaitUntil(() => SubscribedTopics().Contains(EngineCycleReport.InfoTopic(host)));
        SubscribedTopics().Should().Equal(EngineCycleReport.InfoTopic(host));
    }

    [Fact]
    public async Task Dispose_disconnects_and_ends_the_event_subscription()
    {
        // Arrange
        var subscription = Substitute.For<IDisposable>();
        _mediator.Register(Arg.Any<IEventConsumer<ClusterUpdateCompleted>>()).Returns(subscription);
        RunningVersionHas(Guid.NewGuid());
        var sut = CreateCollector(out var hub);
        await using var _ = hub;
        sut.Start();
        await WaitUntil(() => SubscribedTopics().Count > 0);

        // Act
        await sut.DisposeAsync();

        // Assert
        subscription.Received(1).Dispose();
        await _client.Received(1).DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
    }

    private EngineCycleCollector CreateCollector(out EngineCycleHub hub, TimeProvider? timeProvider = null)
    {
        hub = new EngineCycleHub(() => _client, TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        var services = new ServiceCollection()
            .AddScoped(_ => _clusters)
            .AddScoped(_ => _brokerProvider)
            .AddScoped(_ => _mediator)
            .AddSingleton(hub)
            .AddScoped<EngineCycleMonitor>()
            .AddSingleton(Substitute.For<ILogger<EngineCycleMonitor>>())
            .BuildServiceProvider();

        return new EngineCycleCollector(services.GetRequiredService<IServiceScopeFactory>(), timeProvider ?? TimeProvider.System,
            Substitute.For<ILogger<EngineCycleCollector>>());
    }

    private Cluster RunningVersionHas(Guid engineHostId)
    {
        var host = new EngineHost { Id = engineHostId, Engines = [new Engine { Id = Guid.NewGuid(), MinCycleTime = 100 }] };
        var cluster = new Cluster
        {
            Id = Guid.NewGuid(),
            Version = new Version(0, 1, 0),
            Name = "Line",
            NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Applications = [new ClusterApplication { Id = _instance, EngineHosts = [host] }] }] }],
        };
        _clusters.QueryClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns([new ClusterInfo(cluster.Id, cluster.Version, cluster.Name, IsActive: true)]);
        _clusters.Query(cluster.Id, cluster.Version, Arg.Any<CancellationToken>()).Returns(cluster);
        return cluster;
    }

    private static EngineHostBroker Broker()
        => new(Guid.NewGuid(), new MqttConnection { Address = "localhost" },
            new MqttClientOptionsBuilder().WithTcpServer("localhost").Build());

    private List<string> SubscribedTopics()
        => [.. _client.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMqttClient.SubscribeAsync))
            .SelectMany(c => ((MqttClientSubscribeOptions)c.GetArguments()[0]!).TopicFilters)
            .Select(f => f.Topic)];

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20, TestContext.Current.CancellationToken);
    }
}
