using ClusterManagement.Backend.Mqtt;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using ClusterManagement.Backend.Tests.TestUtils;

namespace ClusterManagement.Backend.Tests.Mqtt;

public class MqttNetObserverClientFacts
{
    private const int TestTimeout = 750;

    private IOptions<MqttNetRpcClientOptions> Options { get; }

    public MqttNetObserverClientFacts()
    {
        Options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
        Options.Value
            .Returns(new MqttNetRpcClientOptions() { RequestTimeout = Timeout.InfiniteTimeSpan, });
    }

    public class Observe : MqttNetObserverClientFacts
    {
        [Fact]
        public async Task Throws_ArgumentException_if_topic_is_empty()
        {
            using MqttNetObserverClient client = new(Options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var act = client.Awaiting(_ => _.Observe(_ => Task.CompletedTask, "", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("topic").WithMessage("*topic*invalid*");
        }

        [Fact]
        public async Task Executes_action()
        {
            using MqttNetObserverClient client = new(Options, Substitute.For<IMqttClient>());
            var action = Substitute.For<Func<CancellationToken, Task>>();
            action(TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            try
            {
                await client.Observe(action, "topic", cancellationTokenSource.Token);
            }
            catch { }

            await action.Received(1)(cancellationTokenSource.Token);
        }

        [Fact]
        public async Task Subscribes_and_unsubscribes_topic()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetObserverClient client = new(Options, mqttClient);

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            try
            {
                await client.Observe(_ => Task.CompletedTask, "topic", cancellationTokenSource.Token);
            }
            catch { }

            await mqttClient.Received(1).SubscribeAsync(FluentArg.Assert<MqttClientSubscribeOptions>(
                o =>
                {
                    var subscription = o.TopicFilters.Should().ContainSingle().Subject;
                    subscription.Topic.Should().BeEquivalentTo("topic");
                    subscription.QualityOfServiceLevel.Should().Be(MqttQualityOfServiceLevel.ExactlyOnce);
                }), Arg.Is(cancellationTokenSource.Token));
            await mqttClient.Received(1).UnsubscribeAsync(
                FluentArg.Assert<MqttClientUnsubscribeOptions>(o => o.TopicFilters.Should().ContainSingle("topic")), Arg.Is(cancellationTokenSource.Token));
        }

        [Fact]
        public async Task Returns_content_of_response()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetObserverClient client = new(Options, mqttClient);
            mqttClient.WhenForAnyArgs(_ => _.SubscribeAsync(default))
                .Do(_ => mqttClient.ReceiveApplicationMessage(_ => _.WithTopic("topic").WithPayload("Hello Moritz.")));

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var response = await client.Observe(_ => Task.CompletedTask, "topic", cancellationTokenSource.Token);

            response.Payload.Should().Be("Hello Moritz.");
        }

        [Fact]
        public async Task Throws_TaskCanceledException_if_request_has_a_timeout_as_configured()
        {
            var options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
            options.Value
                .Returns(new MqttNetRpcClientOptions() { RequestTimeout = TimeSpan.FromSeconds(1), });
            using MqttNetObserverClient client = new(options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var act = client.Awaiting(_ => _.Observe(_ => Task.CompletedTask, "topic", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<TaskCanceledException>().WithMessage("*no*message*observed*00:00:01*");
            cancellationTokenSource.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public async Task Throws_TaskCanceledException_if_request_gets_cancelled_before_timeout()
        {
            var options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
            options.Value
                .Returns(new MqttNetRpcClientOptions() { RequestTimeout = TimeSpan.FromSeconds(2), });
            using MqttNetObserverClient client = new(options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var act = client.Awaiting(_ => _.Observe(_ => Task.CompletedTask, "topic", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<TaskCanceledException>().WithMessage("*task*canceled*");
            cancellationTokenSource.IsCancellationRequested.Should().BeTrue();
        }
    }

    public class Dispose : MqttNetObserverClientFacts
    {
        [Fact]
        public async Task Cancels_pending_requests()
        {
            MqttNetObserverClient client = new(Options, Substitute.For<IMqttClient>());
            using CancellationTokenSource cancellationTokenSource = new();

            var requestTask = client.Observe(_ => Task.CompletedTask, "topic", cancellationTokenSource.Token);
            client.Dispose();

            var firstCompletedTask = await Task.WhenAny(requestTask, Task.Delay(TestTimeout, cancellationTokenSource.Token));
            await cancellationTokenSource.CancelAsync();
            firstCompletedTask.Should().BeSameAs(requestTask, "request should be cancelled due to Dispose()");
            await FluentActions.Awaiting(() => requestTask).Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
