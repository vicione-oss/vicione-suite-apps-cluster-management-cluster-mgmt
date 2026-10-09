using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Sdk.Client.Services;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventPageServiceTests
{
    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>().SubscribesEveryEvent();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly FakeTimeProvider _clock = new();

    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();
    private readonly ClusterInfo _clusterInfo = new(Guid.NewGuid(), new Version(1, 0, 1), "Cluster", true);

    private DataflowEventPageService CreateService(params IDataflowEvent[] dataflowEvents)
    {
        _eventBroker.GetEvents().Returns(dataflowEvents);
        _eventBroker.GetCurrentClusterInfo().Returns(_clusterInfo);

        return new(_eventBroker, _bannerService, _clock, Substitute.For<ILogger<DataflowEventPageService>>());
    }

    private async Task<DataflowEventPageService> ShowLoadedEvents(params IDataflowEvent[] dataflowEvents)
    {
        var service = CreateService(dataflowEvents);
        await service.LoadDataflowEvents();
        InlineSynchronizationContext.Run(() => service.ShowEvents(service.Events));
        _clock.Advance(ShownEventSubscriptions.SettleTime);
        return service;
    }

    private int UnsubscribeRequestCount()
        => _eventBroker.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDataflowEventBroker.Unsubscribe));

    private static DataflowEventModel CreateModel(IDataflowEvent dataflowEvent)
        => new()
        {
            EventId = dataflowEvent.EventId,
            Name = dataflowEvent.Name,
            CanRead = dataflowEvent.CanRead,
            CanWrite = dataflowEvent.CanWrite,
            ValueType = dataflowEvent.ValueType,
        };

    public class LoadDataflowEvents : DataflowEventPageServiceTests
    {
        [Fact]
        public async Task Should_load_cluster_events_from_broker()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);

            // Act
            await service.LoadDataflowEvents();

            // Assert
            service.Events.Should().ContainSingle(k => k.CanWrite, "input");
            service.Events.Should().ContainSingle(k => k.CanRead, "output");

            service.ActiveClusterId.Should().Be(_clusterInfo.Id);
            service.ActiveVersion.Should().Be(_clusterInfo.Version);
        }

        [Fact]
        public async Task Should_keep_the_pending_value_of_an_input_across_reloads()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            await service.LoadDataflowEvents();
            service.Events.Single(k => k.EventId == _inputEvent.EventId).PendingValue = "42";

            // Act
            await service.LoadDataflowEvents();

            // Assert
            service.Events.Single(k => k.EventId == _inputEvent.EventId).PendingValue.Should().Be("42");
            service.Events.Single(k => k.EventId == _outputEvent.EventId).PendingValue.Should().BeNull();
        }

        [Fact]
        public async Task Should_deliver_the_values_of_a_shown_output_to_its_model_after_a_reload()
        {
            // Arrange
            var service = await ShowLoadedEvents(_inputEvent, _outputEvent);
            var handler = _eventBroker.SubscribeRequests().Single()[_outputEvent.EventId];

            // Act
            await service.LoadDataflowEvents();
            await handler(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc), "7");

            // Assert
            service.Events.Single(k => k.EventId == _outputEvent.EventId).Value.Should().Be("7");
        }
    }

    public class SetValue : DataflowEventPageServiceTests
    {
        [Fact]
        public async Task Should_set_value_on_event_broker()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            var model = CreateModel(_inputEvent);
            var value = "value";

            // Act
            await service.SetValue(model, value);

            // Assert
            await _eventBroker.Received(1).SetValue(Arg.Is(_inputEvent.EventId), Arg.Is(value), Arg.Any<CancellationToken>());
        }
    }

    public class ShowEvents : DataflowEventPageServiceTests
    {
        [Fact]
        public async Task Should_subscribe_only_the_shown_outputs()
        {
            // Act
            await ShowLoadedEvents(_inputEvent, _outputEvent);

            // Assert
            _eventBroker.SubscribeRequests().Should().ContainSingle().Which.Keys.Should().Equal(_outputEvent.EventId);
        }

        [Fact]
        public async Task Should_request_a_render_when_a_value_arrives()
        {
            // Arrange
            var service = await ShowLoadedEvents(_outputEvent);
            var renders = 0;
            service.RefreshRequested += () =>
            {
                renders++;
                return Task.CompletedTask;
            };

            // Act
            await _eventBroker.SubscribeRequests().Single()[_outputEvent.EventId](new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc), "7");

            // Assert
            renders.Should().Be(1);
            service.Events.Single().Timestamp.Should().Be(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc));
        }
    }

    public class DisposeAsync : DataflowEventPageServiceTests
    {
        [Fact]
        public async Task Should_end_the_subscriptions_once_when_the_page_and_then_its_scope_dispose_it()
        {
            // Arrange
            var service = await ShowLoadedEvents(_outputEvent);

            // Act
            await service.DisposeAsync();
            await service.DisposeAsync();

            // Assert
            UnsubscribeRequestCount().Should().Be(1);
        }

        [Fact]
        public async Task Should_subscribe_again_for_a_page_opened_again()
        {
            // Arrange
            var service = await ShowLoadedEvents(_outputEvent);
            await service.DisposeAsync();

            // Act
            InlineSynchronizationContext.Run(() => service.ShowEvents(service.Events));
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Assert
            _eventBroker.SubscribeRequests().Should().HaveCount(2);
        }
    }
}
