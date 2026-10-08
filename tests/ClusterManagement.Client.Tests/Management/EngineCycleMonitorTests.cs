using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Client.Tests.Management;

public class EngineCycleMonitorTests
{
    private readonly IEngineHostBrokerProvider _brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
    private readonly IMqttClient _client = Substitute.For<IMqttClient>();
    private readonly Guid _instance1 = Guid.NewGuid();
    private readonly Guid _instance2 = Guid.NewGuid();

    public EngineCycleMonitorTests()
        => _client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MqttClientConnectResult())
            .AndDoes(_ => _client.IsConnected.Returns(true));

    [Fact]
    public async Task Watch_skips_engine_hosts_of_an_instance_without_broker()
    {
        // Arrange
        await using var hub = CreateHub();
        await using var sut = new EngineCycleMonitor(_brokerProvider, hub, Substitute.For<ILogger<EngineCycleMonitor>>());
        BrokersAre(new() { [_instance1] = Broker() });
        var host1 = Host(_instance1);

        // Act
        await sut.Watch([host1, Host(_instance2)], TestContext.Current.CancellationToken);

        // Assert
        SubscribedTopics().Should().Equal(EngineCycleReport.InfoTopic(host1.Id));
    }

    [Fact]
    public async Task Watch_reports_brokers_that_cannot_be_found()
    {
        // Arrange
        await using var hub = CreateHub();
        await using var sut = new EngineCycleMonitor(_brokerProvider, hub, Substitute.For<ILogger<EngineCycleMonitor>>());
        _brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<Guid, EngineHostBroker>>(_ => throw new TimeoutException());

        // Act
        var isWatching = await sut.Watch([Host(_instance1)], TestContext.Current.CancellationToken);

        // Assert
        isWatching.Should().BeFalse();
        SubscribedTopics().Should().BeEmpty();
    }

    [Fact]
    public async Task Two_pages_on_the_same_engine_host_share_the_hub_connection()
    {
        // Arrange
        var created = 0;
        await using var hub = new EngineCycleHub(() =>
        {
            created++;
            return _client;
        }, TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var page = new EngineCycleMonitor(_brokerProvider, hub, Substitute.For<ILogger<EngineCycleMonitor>>());
        await using var otherPage = new EngineCycleMonitor(_brokerProvider, hub, Substitute.For<ILogger<EngineCycleMonitor>>());
        BrokersAre(new() { [_instance1] = Broker() });
        var host = Host(_instance1);

        // Act
        await page.Watch([host], TestContext.Current.CancellationToken);
        await otherPage.Watch([host], TestContext.Current.CancellationToken);

        // Assert
        created.Should().Be(1);
        SubscribedTopics().Should().Equal(EngineCycleReport.InfoTopic(host.Id));
        page.Series.Should().BeSameAs(otherPage.Series);
    }

    private EngineCycleHub CreateHub()
        => new(() => _client, TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());

    private static EngineHostRuntimeModel Host(Guid instanceId)
        => new(Guid.NewGuid(), instanceId, "Host", false, []);

    private static EngineHostBroker Broker()
        => new(Guid.NewGuid(), new MqttConnection { Address = "localhost" },
            new MqttClientOptionsBuilder().WithTcpServer("localhost").Build());

    private void BrokersAre(Dictionary<Guid, EngineHostBroker> brokers)
        => _brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(brokers);

    private List<string> SubscribedTopics()
        => [.. _client.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMqttClient.SubscribeAsync))
            .SelectMany(c => ((MqttClientSubscribeOptions)c.GetArguments()[0]!).TopicFilters)
            .Select(f => f.Topic)];
}
