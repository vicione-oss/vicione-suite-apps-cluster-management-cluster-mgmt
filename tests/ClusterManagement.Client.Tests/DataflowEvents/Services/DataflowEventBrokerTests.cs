using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventBrokerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IDataflowEventMqttHandler _mqttHandler = Substitute.For<IDataflowEventMqttHandler>();
    private readonly ILogger<DataflowEventBroker> _logger = Substitute.For<ILogger<DataflowEventBroker>>();
    private readonly Func<DateTime, string?, Task> _handler = Substitute.For<Func<DateTime, string?, Task>>();

    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();

    private DataflowEventBroker CreateEventBroker(params IDataflowEvent[] clusterEvents)
    {
        _mediator.SetupDataflowEventRequest(clusterEvents);
        _mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(Arg.Any<GetClusterInfosRequest>(), CancellationToken.None)
                .Returns(new GetClusterInfosResponse()
                {
                    Results = [new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 5), "Cluster", true)],
                });

        return new(_mediator, _mqttHandler, _logger);
    }

    public class GetEvents : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_load_cluster_events_on_first_call()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var events = await broker.GetEvents(TestContext.Current.CancellationToken);

            // Assert
            events.Should().ContainSingle(k => k.CanWrite, "input");
            events.Should().ContainSingle(k => k.CanRead, "output");
        }
    }

    public class Subscribe : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_load_cluster_events_if_not_cached()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var subscription = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Assert
            subscription.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_subscribe_event_for_first_subscription()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_subscribe_event_for_several_subscriptions()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Value_changed_should_be_called_for_later_subscriptions_with_already_received_data()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, "[some json data]");
            await using var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);
            var valueChanged = false;

            // Act
            await using var subscription2 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

            // Assert
            valueChanged.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[some json data]")]
        public async Task Value_changed_should_be_called_on_payload_received(string? payload)
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, payload);
            var valueChanged = false;
            await using var subscription = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

            // Act
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);

            // Assert
            valueChanged.Should().BeTrue();
        }

        [Fact]
        public async Task Value_changed_should_be_called_on_multiple_handles_on_valid_payload_received()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, "[some json data]");
            var valueChanged1 = false;
            var valueChanged2 = false;
            await using var subscription1 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged1 = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
            await using var subscription2 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged2 = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

            // Act
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);

            // Assert
            valueChanged1.Should().BeTrue();
            valueChanged2.Should().BeTrue();
        }

        private static DataflowEventArgs CreateEventArgs(Guid eventId, string? payload)
        {
            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(eventId.ToString())
                .WithPayload(payload);

            return new DataflowEventArgs(eventId, msgBuilder.Build(), DateTime.UtcNow);
        }
    }

    public class SubscribeOne : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_throw_the_failure_of_the_request()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var failure = new InvalidOperationException("connection lost");
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));

            // Act
            var subscribe = () => broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Assert
            (await subscribe.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        }
    }

    public class Unsubscribe : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_unsubscribe_an_event_that_a_cluster_update_removed()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _mediator.SetupDataflowEventRequest(_inputEvent);
            await broker.Consume(new ClientContext<ClusterUpdateCompleted>(new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 6), TimeSpan.FromSeconds(1)), Guid.NewGuid()), CancellationToken.None);
            _ = await broker.GetEvents(TestContext.Current.CancellationToken);

            // Act
            await subscription.DisposeAsync();

            // Assert
            await _mqttHandler.Received(1).Unsubscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.Single().EventId == _outputEvent.EventId), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_unsubscribe_event_if_subscription_is_remaining()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            await subscription1.DisposeAsync();

            // Assert
            await _mqttHandler.DidNotReceiveWithAnyArgs().Unsubscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_unsubscribe_event_if_no_subscription_is_left()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            var subscription2 = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            await subscription1.DisposeAsync();
            await subscription2.DisposeAsync();

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Unsubscribe(default!, Arg.Any<CancellationToken>());
        }
    }

    public class SubscribeSeveral : DataflowEventBrokerTests
    {
        private readonly IDataflowEvent _otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");

        [Fact]
        public async Task Should_subscribe_the_events_with_one_request()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);

            // Act
            var result = await broker.Subscribe(Handlers(_outputEvent, _otherOutput), TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received(1).Subscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent, _otherOutput })), Arg.Any<CancellationToken>());
            result.Subscriptions.Keys.Should().BeEquivalentTo([_outputEvent.EventId, _otherOutput.EventId]);
            result.Failures.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_leave_an_event_that_is_subscribed_already_out_of_the_request_and_give_its_handler_the_last_value()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);
            await using var earlier = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            var timestamp = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(
                new DataflowEventArgs(_outputEvent.EventId, new MqttApplicationMessageBuilder().WithTopic(_outputEvent.EventId.ToString()).WithPayload("4.2").Build(), timestamp));
            _mqttHandler.ClearReceivedCalls();
            var later = Substitute.For<Func<DateTime, string?, Task>>();

            // Act
            var result = await broker.Subscribe(new Dictionary<Guid, Func<DateTime, string?, Task>>
            {
                [_outputEvent.EventId] = later,
                [_otherOutput.EventId] = _handler,
            }, TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received(1).Subscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _otherOutput })), Arg.Any<CancellationToken>());
            await later.Received(1).Invoke(timestamp, "4.2");
            result.Subscriptions.Keys.Should().BeEquivalentTo([_outputEvent.EventId, _otherOutput.EventId]);
        }

        [Fact]
        public async Task Should_subscribe_the_other_events_when_some_cannot_be_subscribed()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var unknownId = Guid.NewGuid();

            // Act
            var result = await broker.Subscribe(Handlers(_inputEvent, _outputEvent).Append(new(unknownId, _handler)).ToDictionary(),
                TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received(1).Subscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })), Arg.Any<CancellationToken>());
            result.Subscriptions.Keys.Should().Equal(_outputEvent.EventId);
            result.Failures[_inputEvent.EventId].Message.Should().Contain("not readable");
            result.Failures[unknownId].Message.Should().Contain("does not exist");
        }

        [Fact]
        public async Task Should_report_every_event_of_a_failed_request_and_leave_them_unsubscribed()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);
            var failure = new InvalidOperationException("connection lost");
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure), Task.CompletedTask);

            // Act
            var result = await broker.Subscribe(Handlers(_outputEvent, _otherOutput), TestContext.Current.CancellationToken);

            // Assert
            result.Subscriptions.Should().BeEmpty();
            result.Failures.Values.Should().AllSatisfy(f => f.Should().BeSameAs(failure)).And.HaveCount(2);
            _mqttHandler.Received(1).Forget(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })));
            _mqttHandler.Received(1).Forget(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _otherOutput })));
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            await _mqttHandler.Received(1).Subscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_answer_a_handler_that_joined_while_the_request_was_under_way_once_the_request_succeeded()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var request = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>()).Returns(request.Task);
            var first = broker.Subscribe(Handlers(_outputEvent), TestContext.Current.CancellationToken);
            var joined = broker.Subscribe(Handlers(_outputEvent, Substitute.For<Func<DateTime, string?, Task>>()), TestContext.Current.CancellationToken);
            var answeredEarly = joined.IsCompleted;

            // Act
            request.SetResult();

            // Assert
            answeredEarly.Should().BeFalse();
            (await joined).Subscriptions.Keys.Should().Equal(_outputEvent.EventId);
            (await first).Subscriptions.Keys.Should().Equal(_outputEvent.EventId);
            await _mqttHandler.ReceivedWithAnyArgs(1).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_fail_a_handler_that_joined_while_the_request_was_under_way_with_the_request_and_ask_again_later()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var request = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>()).Returns(request.Task, Task.CompletedTask);
            var first = broker.Subscribe(Handlers(_outputEvent), TestContext.Current.CancellationToken);
            var joined = broker.Subscribe(Handlers(_outputEvent, Substitute.For<Func<DateTime, string?, Task>>()), TestContext.Current.CancellationToken);

            // Act
            request.SetException(new InvalidOperationException("connection lost"));
            var results = await Task.WhenAll(first, joined);
            var retry = await broker.Subscribe(Handlers(_outputEvent), TestContext.Current.CancellationToken);

            // Assert
            results.Should().AllSatisfy(r => r.Failures.Keys.Should().Equal(_outputEvent.EventId)).And.AllSatisfy(r => r.Subscriptions.Should().BeEmpty());
            _mqttHandler.Received(1).Forget(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })));
            retry.Subscriptions.Keys.Should().Equal(_outputEvent.EventId);
            await _mqttHandler.ReceivedWithAnyArgs(2).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_report_a_cancelled_request_as_the_failure_of_its_events()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            using var cancellation = new CancellationTokenSource();
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));
            var subscribe = broker.Subscribe(Handlers(_outputEvent), cancellation.Token);

            // Act
            await cancellation.CancelAsync();
            var result = await subscribe;

            // Assert
            result.Failures[_outputEvent.EventId].Should().BeAssignableTo<OperationCanceledException>();
            _mqttHandler.Received(1).Forget(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })));
            await _mqttHandler.Received(1).Unsubscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })), CancellationToken.None);
        }

        [Fact]
        public async Task Should_forget_the_topic_of_a_failed_request_before_it_waits_for_the_other_events()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);
            var otherRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _mqttHandler.Subscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>())
                .Returns(otherRequest.Task, Task.FromException(new InvalidOperationException("connection lost")));
            var other = broker.Subscribe(Handlers(_otherOutput), TestContext.Current.CancellationToken);

            // Act
            var both = broker.Subscribe(new Dictionary<Guid, Func<DateTime, string?, Task>>
            {
                [_outputEvent.EventId] = _handler,
                [_otherOutput.EventId] = Substitute.For<Func<DateTime, string?, Task>>(),
            }, TestContext.Current.CancellationToken);

            // Assert
            _mqttHandler.Received(1).Forget(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _outputEvent })));
            both.IsCompleted.Should().BeFalse();
            otherRequest.SetResult();
            (await both).Subscriptions.Keys.Should().Equal(_otherOutput.EventId);
            _ = await other;
        }

        [Fact]
        public async Task Should_subscribe_a_handler_that_fails_on_the_last_value_and_log_the_failure()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
            await using var earlier = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(
                new DataflowEventArgs(_outputEvent.EventId, new MqttApplicationMessageBuilder().WithTopic(_outputEvent.EventId.ToString()).WithPayload("4.2").Build(), DateTime.UtcNow));
            static Task Failing(DateTime timestamp, string? value) => throw new InvalidOperationException("handler failed");

            // Act
            var result = await broker.Subscribe(new Dictionary<Guid, Func<DateTime, string?, Task>> { [_outputEvent.EventId] = Failing },
                TestContext.Current.CancellationToken);

            // Assert
            result.Subscriptions.Keys.Should().Equal(_outputEvent.EventId);
            _logger.ReceivedCalls().Should().ContainSingle(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && Equals(c.GetArguments()[0], LogLevel.Error));
        }

        private Dictionary<Guid, Func<DateTime, string?, Task>> Handlers(params IDataflowEvent[] events)
            => events.ToDictionary(e => e.EventId, _ => _handler);

        private static Dictionary<Guid, Func<DateTime, string?, Task>> Handlers(IDataflowEvent dataflowEvent, Func<DateTime, string?, Task> handler)
            => new() { [dataflowEvent.EventId] = handler };
    }

    public class UnsubscribeSeveral : DataflowEventBrokerTests
    {
        private readonly IDataflowEvent _otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");

        [Fact]
        public async Task Should_unsubscribe_the_events_with_one_request()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);
            var subscriptions = await broker.Subscribe(new Dictionary<Guid, Func<DateTime, string?, Task>>
            {
                [_outputEvent.EventId] = _handler,
                [_otherOutput.EventId] = _handler,
            }, TestContext.Current.CancellationToken);

            // Act
            await broker.Unsubscribe([.. subscriptions.Subscriptions.Values], TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received(1).Unsubscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.Count == 2 && e.Contains(_outputEvent) && e.Contains(_otherOutput)), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_keep_the_event_another_subscription_still_needs_out_of_the_request()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent, _otherOutput);
            var first = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            var other = await broker.Subscribe(_otherOutput.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            await broker.Unsubscribe([first, other], TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received(1).Unsubscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.SequenceEqual(new[] { _otherOutput })), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_end_a_subscription_only_once()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var first = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            await broker.Unsubscribe([first], TestContext.Current.CancellationToken);
            await first.DisposeAsync();

            // Assert
            await _mqttHandler.DidNotReceiveWithAnyArgs().Unsubscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_end_the_subscriptions_even_when_the_request_fails()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _mqttHandler.Unsubscribe(Arg.Any<IReadOnlyCollection<IDataflowEvent>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("connection lost")));

            // Act
            var unsubscribe = () => broker.Unsubscribe([subscription], TestContext.Current.CancellationToken);

            // Assert
            await unsubscribe.Should().ThrowAsync<InvalidOperationException>();
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(
                new DataflowEventArgs(_outputEvent.EventId, new MqttApplicationMessageBuilder().WithTopic(_outputEvent.EventId.ToString()).WithPayload("4.2").Build(), DateTime.UtcNow));
            await _handler.DidNotReceiveWithAnyArgs().Invoke(default, default);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            await _mqttHandler.ReceivedWithAnyArgs(2).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_refuse_a_subscription_of_another_broker()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var otherBroker = CreateEventBroker(_inputEvent, _outputEvent);
            var foreign = await otherBroker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            var unsubscribe = () => broker.Unsubscribe([foreign], TestContext.Current.CancellationToken);

            // Assert
            await unsubscribe.Should().ThrowAsync<ArgumentException>();
            await _mqttHandler.DidNotReceiveWithAnyArgs().Unsubscribe(default!, Arg.Any<CancellationToken>());
        }
    }

    public class DisposeAsync : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_unsubscribe_every_subscribed_event_with_one_request()
        {
            // Arrange
            var otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");
            var broker = CreateEventBroker(_inputEvent, _outputEvent, otherOutput);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler, TestContext.Current.CancellationToken);
            _ = await broker.Subscribe(otherOutput.EventId, _handler, TestContext.Current.CancellationToken);

            // Act
            await broker.DisposeAsync();

            // Assert
            await _mqttHandler.Received(1).Unsubscribe(Arg.Is<IReadOnlyCollection<IDataflowEvent>>(e => e.Count == 2 && e.Contains(_outputEvent) && e.Contains(otherOutput)), Arg.Any<CancellationToken>());
        }
    }

    public class SetValue : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_call_publish_value_on_mqtt_handler_for_existing_handle()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var value = "[some data]";

            // Act
            await broker.SetValue(_inputEvent.EventId, value, TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received().PublishValue(_inputEvent.EventId, value, _inputEvent.ValueType, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_for_non_writable_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.SetValue(_outputEvent.EventId, "value", TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not writable*");
        }

        [Fact]
        public async Task Should_throw_for_unknown_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.SetValue(Guid.NewGuid(), "value", TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
        }
    }

    public class SubscribeNegativePaths : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_throw_for_non_readable_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.Subscribe(_inputEvent.EventId, _handler);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not readable*");
        }

        [Fact]
        public async Task Should_throw_for_unknown_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.Subscribe(Guid.NewGuid(), _handler);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
        }
    }

    public class ConsumeClusterUpdate : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_invalidate_cache_and_trigger_reconnect()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _ = await broker.GetEvents(TestContext.Current.CancellationToken); // prime cache
            var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1));
            var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());

            // Act
            await broker.Consume(context, CancellationToken.None);

            // Assert — reconnect is triggered on the mqtt handler
            await _mqttHandler.Received(1).ResetAndReconnect(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_reload_events_after_cache_invalidation()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _ = await broker.GetEvents(TestContext.Current.CancellationToken); // prime cache
            var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1));
            var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());

            // Act — Consume fires reconnect; since mock returns synchronously, task completes immediately
            await broker.Consume(context, CancellationToken.None);
            _ = await broker.GetEvents(TestContext.Current.CancellationToken);

            // Assert — mediator should have been called again (2 total: initial + after invalidation)
            await _mediator.Received(2).Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(Arg.Any<GetDataflowEventsRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_tell_its_listeners_once_the_events_of_the_previous_version_are_dropped()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _ = await broker.GetEvents(TestContext.Current.CancellationToken);
            var reloaded = new TaskCompletionSource();
            broker.EventsChanged += async () =>
            {
                _ = await broker.GetEvents(TestContext.Current.CancellationToken);
                reloaded.SetResult();
            };
            var context = new ClientContext<ClusterUpdateCompleted>(
                new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1)), Guid.NewGuid());

            // Act
            await broker.Consume(context, CancellationToken.None);

            // Assert
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await _mediator.Received(2).Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(Arg.Any<GetDataflowEventsRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_tell_every_listener_even_when_one_fails()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var told = new TaskCompletionSource();
            broker.EventsChanged += () => throw new InvalidOperationException("listener failed");
            broker.EventsChanged += () =>
            {
                told.SetResult();
                return Task.CompletedTask;
            };
            var context = new ClientContext<ClusterUpdateCompleted>(
                new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1)), Guid.NewGuid());

            // Act
            await broker.Consume(context, CancellationToken.None);

            // Assert
            await told.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }
}
