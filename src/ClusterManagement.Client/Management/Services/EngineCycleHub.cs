using ClusterManagement.Client.Management.Models;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// Collects the cycle reports engine hosts publish into <see cref="Series"/> for every watcher at once: each broker is
/// connected once and each engine host subscribed once, however many watchers ask for it.
/// </summary>
/// <remarks>
/// Engine hosts publish a report on every cycle, so one connection per watcher would multiply that load by the number
/// of open pages.
/// </remarks>
internal sealed partial class EngineCycleHub(Func<IMqttClient> createClient, TimeProvider timeProvider, ILogger<EngineCycleHub> logger)
    : IAsyncDisposable
{
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_connectTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _watchLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, BrokerClient> _clients = [];
    private readonly Dictionary<object, IReadOnlyDictionary<Guid, EngineHostBroker>> _watchers = [];
    private int _retryScheduled;

    public CycleTimeSeries Series { get; } = new();

    /// <summary>
    /// Subscribes <paramref name="watcher"/> to exactly these engine hosts, each on its broker. Until the last watcher
    /// is released, a broker that cannot be reached or drops the connection is retried every few seconds.
    /// </summary>
    public async Task Watch(object watcher, IReadOnlyDictionary<Guid, EngineHostBroker> brokerByEngineHost, CancellationToken cancellationToken)
    {
        await _watchLock.WaitAsync(cancellationToken);
        try
        {
            _watchers[watcher] = brokerByEngineHost;
        }
        finally
        {
            _watchLock.Release();
        }

        await Subscribe(cancellationToken);
    }

    /// <summary>
    /// Ends the subscriptions of <paramref name="watcher"/>; with the last watcher every broker is disconnected and the
    /// collected cycles are forgotten.
    /// </summary>
    public async Task Release(object watcher)
    {
        await _watchLock.WaitAsync();
        try
        {
            if (!_watchers.Remove(watcher))
                return;
        }
        finally
        {
            _watchLock.Release();
        }

        await Subscribe(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();

        foreach (var client in _clients.Values)
        {
            client.Mqtt.ApplicationMessageReceivedAsync -= OnMessageReceived;
            client.Mqtt.DisconnectedAsync -= OnDisconnected;
            await Disconnect(client);
            client.Dispose();
        }

        _watchLock.Dispose();
        _lifetime.Dispose();
    }

    private async Task Subscribe(CancellationToken cancellationToken)
    {
        bool isComplete;
        try
        {
            // A sync also applies what other watchers asked for meanwhile, so it runs on the hub's lifetime and not on
            // the token of the watcher that happened to start it.
            var clients = await ClientsToSync(cancellationToken);
            var results = await Task.WhenAll(clients.Select(client => SyncBroker(client, _lifetime.Token)));
            isComplete = results.All(result => result);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogWatchFailed(logger, e);
            isComplete = false;
        }

        if (!isComplete)
            RetryLater();
    }

    // Every client with a broker that is watched, plus every client whose broker is no longer watched and is disconnected.
    private async Task<List<BrokerClient>> ClientsToSync(CancellationToken cancellationToken)
    {
        await _watchLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var connectionId in TopicsByBroker(_watchers.Values).Keys)
                ClientFor(connectionId);

            if (_watchers.Count == 0)
                Series.Clear();

            return [.. _clients.Values];
        }
        finally
        {
            _watchLock.Release();
        }
    }

    private static Dictionary<Guid, (EngineHostBroker Broker, HashSet<string> Topics)> TopicsByBroker(
        IEnumerable<IReadOnlyDictionary<Guid, EngineHostBroker>> watchers)
    {
        var topicsByBroker = new Dictionary<Guid, (EngineHostBroker Broker, HashSet<string> Topics)>();
        foreach (var (engineHostId, broker) in watchers.SelectMany(w => w))
        {
            if (!topicsByBroker.TryGetValue(broker.ConnectionId, out var entry))
                topicsByBroker[broker.ConnectionId] = entry = (broker, []);

            entry.Topics.Add(EngineCycleReport.InfoTopic(engineHostId));
        }

        return topicsByBroker;
    }

    /// <summary>
    /// Brings one broker connection to what the watchers want. Each broker has its own lock and connect timeout, and a
    /// caller never waits for a sync already running on that broker: the running sync applies the change before it
    /// ends. So one broker that cannot be reached holds up neither the other brokers nor the watchers.
    /// </summary>
    private async Task<bool> SyncBroker(BrokerClient client, CancellationToken cancellationToken)
    {
        Volatile.Write(ref client.SyncRequested, 1);
        if (!await client.Lock.WaitAsync(0, cancellationToken))
            return true;

        bool isSynced;
        try
        {
            while (Interlocked.Exchange(ref client.SyncRequested, 0) == 1)
                await SyncBrokerOnce(client, cancellationToken);

            isSynced = true;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogBrokerFailed(logger, e, client.ConnectionId);
            isSynced = false;
        }
        finally
        {
            client.Lock.Release();
        }

        // A change requested after the last pass but before the lock was released would otherwise be lost.
        return isSynced && Volatile.Read(ref client.SyncRequested) == 1
            ? await SyncBroker(client, cancellationToken)
            : isSynced;
    }

    private async Task SyncBrokerOnce(BrokerClient client, CancellationToken cancellationToken)
    {
        var (broker, topics) = await WantedOn(client.ConnectionId, cancellationToken);
        if (client.ConnectedTo != broker?.Connection)
            await Disconnect(client);
        if (broker is not null)
            await ConnectAndSubscribe(client, broker, topics, cancellationToken);
    }

    private async Task<(EngineHostBroker? Broker, HashSet<string> Topics)> WantedOn(Guid connectionId, CancellationToken cancellationToken)
    {
        await _watchLock.WaitAsync(cancellationToken);
        try
        {
            return TopicsByBroker(_watchers.Values).TryGetValue(connectionId, out var wanted) ? wanted : (null, []);
        }
        finally
        {
            _watchLock.Release();
        }
    }

    private static async Task ConnectAndSubscribe(BrokerClient client, EngineHostBroker broker, HashSet<string> topics,
        CancellationToken cancellationToken)
    {
        if (!client.Mqtt.IsConnected)
        {
            client.Topics.Clear();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(s_connectTimeout);
            await client.Mqtt.ConnectAsync(broker.Options, connectTimeout.Token);
            client.ConnectedTo = broker.Connection;
        }

        foreach (var topic in client.Topics.Except(topics).ToList())
        {
            await client.Mqtt.UnsubscribeAsync(topic, cancellationToken);
            client.Topics.Remove(topic);
        }

        foreach (var topic in topics.Except(client.Topics).ToList())
        {
            await client.Mqtt.SubscribeAsync(topic, cancellationToken: cancellationToken);
            client.Topics.Add(topic);
        }
    }

    private BrokerClient ClientFor(Guid connectionId)
    {
        if (_clients.TryGetValue(connectionId, out var client))
            return client;

        client = new BrokerClient(connectionId, createClient());
        client.Mqtt.ApplicationMessageReceivedAsync += OnMessageReceived;
        client.Mqtt.DisconnectedAsync += OnDisconnected;
        _clients[connectionId] = client;
        return client;
    }

    private async Task Disconnect(BrokerClient client)
    {
        client.Topics.Clear();
        client.ConnectedTo = null;
        try
        {
            if (client.Mqtt.IsConnected)
                await client.Mqtt.DisconnectAsync();
        }
        catch (Exception e)
        {
            LogStopFailed(logger, e);
        }
    }

    // A failed connect attempt raises the event as well; that one is retried by Subscribe.
    private Task OnDisconnected(MqttClientDisconnectedEventArgs args)
    {
        if (args.ClientWasConnected)
            RetryLater();

        return Task.CompletedTask;
    }

    private void RetryLater()
    {
        if (Interlocked.Exchange(ref _retryScheduled, 1) == 1)
            return;

        _ = RetryAfterDelay(_lifetime.Token);
    }

    private async Task RetryAfterDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_retryDelay, timeProvider, cancellationToken);
            Volatile.Write(ref _retryScheduled, 0);
            await Subscribe(cancellationToken);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The hub was disposed.
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        if (!EngineCycleReport.TryParseEngineHostId(args.ApplicationMessage.Topic, out var engineHostId))
            return Task.CompletedTask;

        if (EngineCycleReport.TryParse(args.ApplicationMessage.Payload) is { } report)
            Series.Add(engineHostId, report, timeProvider.GetUtcNow());

        return Task.CompletedTask;
    }

    private sealed class BrokerClient(Guid connectionId, IMqttClient mqtt) : IDisposable
    {
        public Guid ConnectionId { get; } = connectionId;
        public IMqttClient Mqtt { get; } = mqtt;

        /// <summary>
        /// Held while the connection and its subscriptions are changed; <see cref="Topics"/> is only touched under it.
        /// </summary>
        public SemaphoreSlim Lock { get; } = new(1, 1);

        /// <summary>
        /// 1 while a change waits to be applied by the sync that holds <see cref="Lock"/>.
        /// </summary>
        public int SyncRequested;

        public HashSet<string> Topics { get; } = [];

        /// <summary>
        /// The broker settings of the last connect, kept after the broker drops the connection; a reconnect uses the
        /// settings the watchers want now.
        /// </summary>
        public MqttConnection? ConnectedTo { get; set; }

        public void Dispose()
        {
            Mqtt.Dispose();
            Lock.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to subscribe to engine host cycle reports")]
    private static partial void LogWatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to subscribe to engine host cycle reports on broker connection {ConnectionId}")]
    private static partial void LogBrokerFailed(ILogger logger, Exception exception, Guid connectionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to stop watching engine host cycle reports")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
