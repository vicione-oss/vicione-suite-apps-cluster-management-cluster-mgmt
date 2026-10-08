using System.Text;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Connections.Contracts;
using MQTTnet.Packets;

namespace ClusterManagement.Client.Tests.Management;

public class EngineCycleHubTests
{
    private readonly IMqttClient _client = Substitute.For<IMqttClient>();
    private readonly Queue<IMqttClient> _clients = new();
    private readonly EngineHostBroker _broker = Broker();
    private readonly Guid _host1 = Guid.NewGuid();
    private readonly Guid _host2 = Guid.NewGuid();
    private readonly object _page = new();
    private readonly object _otherPage = new();

    public EngineCycleHubTests()
        => ConnectsOnFirstAttempt(_client);

    [Fact]
    public async Task Watch_subscribes_to_the_cycle_topic_of_every_engine_host()
    {
        // Arrange
        await using var sut = CreateHub();

        // Act
        await sut.Watch(_page, On(_broker, _host1, _host2), TestContext.Current.CancellationToken);

        // Assert
        SubscribedTopics(_client).Should().BeEquivalentTo(EngineCycleReport.InfoTopic(_host1), EngineCycleReport.InfoTopic(_host2));
    }

    [Fact]
    public async Task Watch_subscribes_each_engine_host_on_its_broker()
    {
        // Arrange
        var client2 = Substitute.For<IMqttClient>();
        ConnectsOnFirstAttempt(client2);
        _clients.Enqueue(_client);
        _clients.Enqueue(client2);
        await using var sut = CreateHub();

        // Act
        await sut.Watch(_page, new Dictionary<Guid, EngineHostBroker> { [_host1] = _broker, [_host2] = Broker() }, TestContext.Current.CancellationToken);

        // Assert
        SubscribedTopics(_client).Should().Equal(EngineCycleReport.InfoTopic(_host1));
        SubscribedTopics(client2).Should().Equal(EngineCycleReport.InfoTopic(_host2));
    }

    [Fact]
    public async Task Pages_watching_the_same_engine_host_share_one_connection_and_subscription()
    {
        // Arrange
        var created = 0;
        await using var sut = CreateHub(() =>
        {
            created++;
            return _client;
        });

        // Act
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        await sut.Watch(_otherPage, On(_broker with { }, _host1), TestContext.Current.CancellationToken);

        // Assert
        created.Should().Be(1);
        SubscribedTopics(_client).Should().Equal(EngineCycleReport.InfoTopic(_host1));
    }

    [Fact]
    public async Task Releasing_one_page_keeps_what_another_page_watches()
    {
        // Arrange
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1, _host2), TestContext.Current.CancellationToken);
        await sut.Watch(_otherPage, On(_broker, _host1), TestContext.Current.CancellationToken);
        _client.ClearReceivedCalls();

        // Act
        await sut.Release(_page);

        // Assert
        await _client.Received(1).UnsubscribeAsync(
            Arg.Is<MqttClientUnsubscribeOptions>(o => o.TopicFilters.Single() == EngineCycleReport.InfoTopic(_host2)),
            Arg.Any<CancellationToken>());
        await _client.DidNotReceive().DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Releasing_the_last_page_disconnects_and_forgets_the_collected_cycles()
    {
        // Arrange
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        Receive(EngineCycleReport.InfoTopic(_host1), """{"CycleDuration":4.8,"Deployments":[]}""");

        // Act
        await sut.Release(_page);

        // Assert
        await _client.Received(1).DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
        sut.Series.Latest(_host1, DateTimeOffset.UtcNow).Should().BeNull();
    }

    [Fact]
    public async Task Reconnects_with_the_new_settings_when_a_broker_changes_under_the_same_connection()
    {
        // Arrange
        _client.When(c => c.DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => _client.IsConnected.Returns(false));
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        var moved = _broker with
        {
            Connection = new MqttConnection { Address = "other-host" },
            Options = new MqttClientOptionsBuilder().WithTcpServer("other-host").Build(),
        };
        _client.ClearReceivedCalls();

        // Act
        await sut.Watch(_page, On(moved, _host1), TestContext.Current.CancellationToken);

        // Assert
        await _client.Received(1).DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
        await _client.Received(1).ConnectAsync(moved.Options, Arg.Any<CancellationToken>());
        SubscribedTopics(_client).Should().Equal(EngineCycleReport.InfoTopic(_host1));
    }

    [Fact]
    public async Task Keeps_the_connection_when_a_broker_is_looked_up_again_with_the_same_settings()
    {
        // Arrange
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        var lookedUpAgain = _broker with
        {
            Connection = new MqttConnection { Address = "localhost" },
            Options = new MqttClientOptionsBuilder().WithTcpServer("localhost").Build(),
        };
        _client.ClearReceivedCalls();

        // Act
        await sut.Watch(_page, On(lookedUpAgain, _host1), TestContext.Current.CancellationToken);

        // Assert
        await _client.DidNotReceive().DisconnectAsync(Arg.Any<MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceive().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_broker_that_hangs_while_connecting_holds_up_neither_other_brokers_nor_other_pages()
    {
        // Arrange
        var hanging = Substitute.For<IMqttClient>();
        hanging.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitUntilCancelled(call.Arg<CancellationToken>()));
        _clients.Enqueue(hanging);
        _clients.Enqueue(_client);
        var reachable = Broker();
        await using var sut = CreateHub();
        var slowWatch = sut.Watch(_page, new Dictionary<Guid, EngineHostBroker> { [_host1] = _broker, [_host2] = reachable },
            TestContext.Current.CancellationToken);

        // Act
        var otherWatch = sut.Watch(_otherPage, On(reachable, _host2), TestContext.Current.CancellationToken);

        // Assert
        await otherWatch.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        SubscribedTopics(_client).Should().Contain(EngineCycleReport.InfoTopic(_host2));
        slowWatch.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_page_closing_during_a_slow_connect_keeps_what_another_page_asked_for()
    {
        // Arrange
        var connected = new TaskCompletionSource();
        _client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await connected.Task.WaitAsync(call.Arg<CancellationToken>());
                _client.IsConnected.Returns(true);
                return new MqttClientConnectResult();
            });
        await using var sut = CreateHub();
        using var pageClosed = new CancellationTokenSource();
        var pageWatch = sut.Watch(_page, On(_broker, _host1), pageClosed.Token);
        await sut.Watch(_otherPage, On(_broker, _host2), TestContext.Current.CancellationToken);

        // Act
        await pageClosed.CancelAsync();
        connected.SetResult();

        // Assert
        await pageWatch.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await WaitUntil(() => SubscribedTopics(_client).Contains(EngineCycleReport.InfoTopic(_host2)));
        SubscribedTopics(_client).Should().Contain(EngineCycleReport.InfoTopic(_host2));
    }

    [Fact]
    public async Task Watch_subscribes_again_after_the_connection_was_lost()
    {
        // Arrange
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        _client.IsConnected.Returns(false);
        _client.ClearReceivedCalls();

        // Act
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);

        // Assert
        await _client.Received(1).ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        SubscribedTopics(_client).Should().ContainSingle().Which.Should().Be(EngineCycleReport.InfoTopic(_host1));
    }

    [Fact]
    public async Task Watch_survives_a_broker_that_cannot_be_reached()
    {
        // Arrange
        _client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns<MqttClientConnectResult>(_ => throw new InvalidOperationException("Error while connecting host"));
        await using var sut = CreateHub();

        // Act
        var act = () => sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        SubscribedTopics(_client).Should().BeEmpty();
    }

    [Fact]
    public async Task Retries_a_broker_that_could_not_be_reached()
    {
        // Arrange
        var attempts = 0;
        _client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++attempts == 1)
                throw new InvalidOperationException("Error while connecting host");

            _client.IsConnected.Returns(true);
            return new MqttClientConnectResult();
        });
        await using var sut = CreateHub(timeProvider: new ImmediateTimeProvider());

        // Act
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);

        // Assert
        await WaitUntil(() => SubscribedTopics(_client).Count > 0);
        SubscribedTopics(_client).Should().Equal(EngineCycleReport.InfoTopic(_host1));
    }

    [Fact]
    public async Task Subscribes_again_after_the_broker_dropped_the_connection()
    {
        // Arrange
        await using var sut = CreateHub(timeProvider: new ImmediateTimeProvider());
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        _client.IsConnected.Returns(false);
        _client.ClearReceivedCalls();

        // Act
        DropConnection();

        // Assert
        await WaitUntil(() => SubscribedTopics(_client).Count > 0);
        await _client.Received(1).ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        SubscribedTopics(_client).Should().ContainSingle().Which.Should().Be(EngineCycleReport.InfoTopic(_host1));
    }

    [Fact]
    public async Task Does_not_reconnect_after_the_last_page_is_released()
    {
        // Arrange
        await using var sut = CreateHub(timeProvider: new ImmediateTimeProvider());
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);
        await sut.Release(_page);
        _client.IsConnected.Returns(false);
        _client.ClearReceivedCalls();

        // Act
        DropConnection();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        await _client.DidNotReceive().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Records_the_chain_and_the_engine_cycles_of_a_report()
    {
        // Arrange
        var deploymentId = Guid.NewGuid();
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);

        // Act
        Receive(EngineCycleReport.InfoTopic(_host1), $$"""{"CycleDuration":4.8,"Deployments":[{"Deployment":"{{deploymentId}}","Duration":2.1,"Cycle":7}]}""");

        // Assert
        sut.Series.Latest(_host1, DateTimeOffset.UtcNow).Should().Be(4.8);
        sut.Series.Latest(deploymentId, DateTimeOffset.UtcNow).Should().Be(2.1);
    }

    [Theory]
    [InlineData("data/42")]
    [InlineData("8b6f0a4e-2a4f-4c5e-9b1a-0d7c0e3f1a22/cycle/crash")]
    public async Task Ignores_messages_on_other_topics(string topic)
    {
        // Arrange
        await using var sut = CreateHub();
        await sut.Watch(_page, On(_broker, _host1), TestContext.Current.CancellationToken);

        // Act
        Receive(topic, """{"CycleDuration":4.8,"Deployments":[]}""");

        // Assert
        sut.Series.Latest(Guid.Parse("8b6f0a4e-2a4f-4c5e-9b1a-0d7c0e3f1a22"), DateTimeOffset.UtcNow).Should().BeNull();
    }

    private EngineCycleHub CreateHub(Func<IMqttClient>? createClient = null, TimeProvider? timeProvider = null)
        => new(createClient ?? (() => _clients.Count > 0 ? _clients.Dequeue() : _client), timeProvider ?? TimeProvider.System,
            Substitute.For<ILogger<EngineCycleHub>>());

    private static Dictionary<Guid, EngineHostBroker> On(EngineHostBroker broker, params Guid[] engineHosts)
        => engineHosts.ToDictionary(host => host, _ => broker);

    private static EngineHostBroker Broker()
        => new(Guid.NewGuid(), new MqttConnection { Address = "localhost" },
            new MqttClientOptionsBuilder().WithTcpServer("localhost").Build());

    private static void ConnectsOnFirstAttempt(IMqttClient client)
        => client.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MqttClientConnectResult())
            .AndDoes(_ => client.IsConnected.Returns(true));

    private static List<string> SubscribedTopics(IMqttClient client)
        => [.. client.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMqttClient.SubscribeAsync))
            .SelectMany(c => ((MqttClientSubscribeOptions)c.GetArguments()[0]!).TopicFilters)
            .Select(f => f.Topic)];

    private void Receive(string topic, string json)
    {
        var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(Encoding.UTF8.GetBytes(json)).Build();
        var args = new MqttApplicationMessageReceivedEventArgs("client", message, new MqttPublishPacket(), (_, _) => Task.CompletedTask);
        _client.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(args);
    }

    private void DropConnection()
    {
        var args = new MqttClientDisconnectedEventArgs(true, null, MqttClientDisconnectReason.UnspecifiedError, "broker restarted", [], null);
        _client.DisconnectedAsync += Raise.Event<Func<MqttClientDisconnectedEventArgs, Task>>(args);
    }

    private static async Task<MqttClientConnectResult> WaitUntilCancelled(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new MqttClientConnectResult();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20, TestContext.Current.CancellationToken);
    }
}
