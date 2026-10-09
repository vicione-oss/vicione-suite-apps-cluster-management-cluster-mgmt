using ClusterManagement.Client.DataflowEvents.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class ShownEventSubscriptionsTests
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMilliseconds(1);

    private readonly IDataflowEventBroker _broker = Substitute.For<IDataflowEventBroker>().SubscribesEveryEvent();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly FakeTimeProvider _clock = new();
    private readonly Guid _first = Guid.NewGuid();
    private readonly Guid _second = Guid.NewGuid();
    private readonly Guid _third = Guid.NewGuid();

    private ShownEventSubscriptions Create()
        => new(_broker, _ => (_, _) => Task.CompletedTask, _clock, _logger);

    private IAsyncDisposable ShowAndSubscribe(ShownEventSubscriptions sut, Guid eventId)
    {
        var subscription = Substitute.For<IAsyncDisposable>();
        _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>())
            .Returns(Subscribed(eventId, subscription));
        ShowInView(sut, eventId);
        _clock.Advance(ShownEventSubscriptions.SettleTime);
        return subscription;
    }

    // Like the page's renderer, the code under test continues where it started.
    private static void ShowInView(ShownEventSubscriptions sut, params Guid[] eventIds)
        => InlineSynchronizationContext.Run(() => sut.Show(eventIds));

    private static DataflowEventSubscriptions Subscribed(Guid eventId, IAsyncDisposable subscription)
        => new(new Dictionary<Guid, IAsyncDisposable> { [eventId] = subscription }, new Dictionary<Guid, Exception>());

    private List<Guid[]> SubscribedEvents()
        => [.. _broker.SubscribeRequests().Select(handlers => handlers.Keys.ToArray())];

    private List<IAsyncDisposable[]> EndedSubscriptions()
        => [.. _broker.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IDataflowEventBroker.Unsubscribe))
            .Select(c => ((IReadOnlyCollection<IAsyncDisposable>)c.GetArguments()[0]!).ToArray())];

    public class Show : ShownEventSubscriptionsTests
    {
        [Fact]
        public async Task Should_subscribe_the_shown_events_once_they_have_held_for_the_settle_time()
        {
            // Arrange
            await using var sut = Create();
            ShowInView(sut, _first, _second);

            // Act
            _clock.Advance(ShownEventSubscriptions.SettleTime - s_tick);
            var early = SubscribedEvents();
            _clock.Advance(s_tick);

            // Assert
            early.Should().BeEmpty();
            SubscribedEvents().Should().ContainSingle().Which.Should().BeEquivalentTo([_first, _second]);
        }

        [Fact]
        public async Task Should_subscribe_with_one_request_what_is_shown_once_scrolling_has_settled()
        {
            // Arrange
            await using var sut = Create();

            // Act
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime / 3);
            ShowInView(sut, _first, _second);
            _clock.Advance(ShownEventSubscriptions.SettleTime / 3);
            ShowInView(sut, _second, _third);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Assert
            SubscribedEvents().Should().ContainSingle().Which.Should().BeEquivalentTo([_second, _third]);
        }

        [Fact]
        public async Task Should_end_the_subscription_of_an_event_that_stays_out_of_view_for_the_keep_time()
        {
            // Arrange
            await using var sut = Create();
            var subscription = ShowAndSubscribe(sut, _first);
            ShowInView(sut);

            // Act
            _clock.Advance(ShownEventSubscriptions.KeepTime - s_tick);
            var early = EndedSubscriptions();
            _clock.Advance(s_tick);

            // Assert
            early.Should().BeEmpty();
            EndedSubscriptions().Should().ContainSingle().Which.Should().Equal(subscription);
        }

        [Fact]
        public async Task Should_subscribe_newly_shown_events_while_one_that_left_the_view_waits_out_its_keep_time()
        {
            // Arrange
            await using var sut = Create();
            ShowAndSubscribe(sut, _first);
            _broker.SubscribesEveryEvent();
            ShowInView(sut);
            _clock.Advance(ShownEventSubscriptions.KeepTime / 5);

            // Act
            ShowInView(sut, _second);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Assert
            SubscribedEvents().Should().SatisfyRespectively(
                ids => ids.Should().Equal(_first),
                ids => ids.Should().Equal(_second));
        }

        [Fact]
        public async Task Should_keep_the_subscription_of_an_event_that_comes_back_in_time()
        {
            // Arrange
            await using var sut = Create();
            ShowAndSubscribe(sut, _first);
            ShowInView(sut);
            _clock.Advance(ShownEventSubscriptions.KeepTime / 2);

            // Act
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.KeepTime * 2);

            // Assert
            EndedSubscriptions().Should().BeEmpty();
            SubscribedEvents().Should().ContainSingle();
        }

        [Fact]
        public async Task Should_ask_again_for_an_event_that_could_not_be_subscribed()
        {
            // Arrange
            _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>())
                .Returns(
                    new DataflowEventSubscriptions(new Dictionary<Guid, IAsyncDisposable>(), new Dictionary<Guid, Exception> { [_first] = new TimeoutException() }),
                    Subscribed(_first, Substitute.For<IAsyncDisposable>()));
            await using var sut = Create();
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Act
            _clock.Advance(ShownEventSubscriptions.RetryTime - s_tick);
            var early = SubscribedEvents();
            _clock.Advance(s_tick);

            // Assert
            early.Should().HaveCount(1);
            SubscribedEvents().Should().HaveCount(2).And.AllSatisfy(ids => ids.Should().Equal(_first));
        }

        [Fact]
        public async Task Should_subscribe_an_event_shown_after_a_failure_without_waiting_for_its_retry()
        {
            // Arrange
            _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => new DataflowEventSubscriptions(new Dictionary<Guid, IAsyncDisposable>(), new Dictionary<Guid, Exception> { [_first] = new TimeoutException() }),
                    call => new DataflowEventSubscriptions(
                        call.Arg<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>().ToDictionary(h => h.Key, _ => Substitute.For<IAsyncDisposable>()),
                        new Dictionary<Guid, Exception>()));
            await using var sut = Create();
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Act
            ShowInView(sut, _first, _second);
            _clock.Advance(ShownEventSubscriptions.SettleTime);
            var beforeRetry = SubscribedEvents();
            _clock.Advance(ShownEventSubscriptions.RetryTime);

            // Assert
            beforeRetry.Should().SatisfyRespectively(
                ids => ids.Should().Equal(_first),
                ids => ids.Should().Equal(_second));
            SubscribedEvents().Should().HaveCount(3);
            SubscribedEvents()[2].Should().Equal(_first);
        }

        [Fact]
        public async Task Should_ask_again_after_a_failed_request()
        {
            // Arrange
            _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => throw new TimeoutException(),
                    _ => Subscribed(_first, Substitute.For<IAsyncDisposable>()));
            await using var sut = Create();
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Act
            _clock.Advance(ShownEventSubscriptions.RetryTime);

            // Assert
            SubscribedEvents().Should().HaveCount(2);
        }

        [Fact]
        public async Task Should_end_the_subscription_of_an_event_that_left_the_view_while_it_was_under_way_only_once_it_is_done()
        {
            // Arrange
            var answer = new TaskCompletionSource<DataflowEventSubscriptions>();
            _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>()).Returns(answer.Task);
            await using var sut = Create();
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime);
            ShowInView(sut);
            _clock.Advance(ShownEventSubscriptions.KeepTime * 2);
            var whileUnderWay = EndedSubscriptions();
            var subscription = Substitute.For<IAsyncDisposable>();

            // Act
            answer.SetResult(Subscribed(_first, subscription));
            _clock.Advance(ShownEventSubscriptions.KeepTime);

            // Assert
            whileUnderWay.Should().BeEmpty();
            EndedSubscriptions().Should().ContainSingle().Which.Should().Equal(subscription);
        }
    }

    public class DisposeAsync : ShownEventSubscriptionsTests
    {
        [Fact]
        public async Task Should_end_every_subscription()
        {
            // Arrange
            var sut = Create();
            var subscription = ShowAndSubscribe(sut, _first);

            // Act
            await sut.DisposeAsync();

            // Assert
            EndedSubscriptions().Should().ContainSingle().Which.Should().Equal(subscription);
        }

        [Fact]
        public async Task Should_neither_warn_nor_ask_again_about_the_request_it_cancelled()
        {
            // Arrange
            _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
            _broker.Subscribe(Arg.Any<IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>>>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()).ContinueWith(
                    _ => new DataflowEventSubscriptions(new Dictionary<Guid, IAsyncDisposable>(), new Dictionary<Guid, Exception> { [_first] = new OperationCanceledException() }),
                    TaskScheduler.Default));
            var sut = Create();
            ShowInView(sut, _first);
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Act
            await sut.DisposeAsync();
            _clock.Advance(ShownEventSubscriptions.RetryTime);

            // Assert
            _logger.ReceivedCalls().Should().NotContain(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
            SubscribedEvents().Should().ContainSingle();
        }

        [Fact]
        public async Task Should_subscribe_nothing_that_was_still_settling()
        {
            // Arrange
            var sut = Create();
            ShowInView(sut, _first);

            // Act
            await sut.DisposeAsync();
            _clock.Advance(ShownEventSubscriptions.SettleTime);

            // Assert
            SubscribedEvents().Should().BeEmpty();
        }
    }
}
