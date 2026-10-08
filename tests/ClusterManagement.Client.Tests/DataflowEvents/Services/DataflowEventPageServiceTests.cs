using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventPageServiceTests
{
    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();

    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();
    private readonly ClusterInfo _clusterInfo = new(Guid.NewGuid(), new Version(1, 0, 1), "Cluster", true);

    private DataflowEventPageService CreateService(params IDataflowEvent[] dataflowEvents)
    {
        _eventBroker.GetEvents().Returns(dataflowEvents);
        _eventBroker.GetCurrentClusterInfo().Returns(_clusterInfo);

        return new(_eventBroker, _bannerService, Substitute.For<ILogger<DataflowEventPageService>>());
    }

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "Overload method")]
        public async Task Should_keep_the_subscription_of_an_event_across_reloads_and_deliver_its_values_to_the_new_model()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            Func<DateTime, string?, Task>? handler = null;
            var subscription = Substitute.For<IAsyncDisposable>();
            _eventBroker.Subscribe(_outputEvent.EventId, Arg.Do<Func<DateTime, string?, Task>>(h => handler = h)).Returns(subscription);
            await service.LoadDataflowEvents();
            await service.SubscribeEvent(service.Events.Single(k => k.EventId == _outputEvent.EventId));

            // Act
            await service.LoadDataflowEvents();
            await handler!(new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc), "7");

            // Assert
            var reloaded = service.Events.Single(k => k.EventId == _outputEvent.EventId);
            reloaded.Subscription.Should().BeSameAs(subscription);
            reloaded.Value.Should().Be("7");
            await subscription.DidNotReceive().DisposeAsync();
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "Overload method")]
        public async Task Should_end_the_subscription_of_an_event_that_is_gone_after_a_reload()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            var subscription = Substitute.For<IAsyncDisposable>();
            _eventBroker.Subscribe(_outputEvent.EventId, Arg.Any<Func<DateTime, string?, Task>>()).Returns(subscription);
            await service.LoadDataflowEvents();
            await service.SubscribeEvent(service.Events.Single(k => k.EventId == _outputEvent.EventId));
            _eventBroker.GetEvents().Returns([_inputEvent]);

            // Act
            await service.LoadDataflowEvents();

            // Assert
            await subscription.Received(1).DisposeAsync();
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

    public class SubscribeEvent : DataflowEventPageServiceTests
    {
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken", Justification = "Overload method")]
        public async Task Should_add_subscription_to_model()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            var model = CreateModel(_outputEvent);

            // Act
            await service.SubscribeEvent(model);

            // Assert
            await _eventBroker.Received(1).Subscribe(Arg.Is(_outputEvent.EventId), Arg.Any<Func<DateTime, string?, Task>>());
            model.Subscription.Should().NotBeNull();
        }
    }

    public class UnsubscribeEvent : DataflowEventPageServiceTests
    {
        [Fact]
        public async Task Should_dispose_and_remove_subscription()
        {
            // Arrange
            var service = CreateService(_inputEvent, _outputEvent);
            var model = CreateModel(_outputEvent);
            model.Subscription = Substitute.For<IAsyncDisposable>();
            var subscription = model.Subscription;

            // Act
            await service.UnsubscribeEvent(model);

            // Assert
            model.Subscription.Should().BeNull();
            await subscription!.Received(1).DisposeAsync();
        }
    }
}
