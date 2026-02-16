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
            service.ActiveVersion.Should().Be($"v{_clusterInfo.Version}");
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
