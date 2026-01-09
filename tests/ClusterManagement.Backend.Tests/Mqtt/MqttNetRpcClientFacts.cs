using System.Net.Mime;
using System.Text;
using ClusterManagement.Backend.Mqtt;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using NSubstitute;
using Xunit;
using ClusterManagement.Backend.Tests.TestUtils;

namespace ClusterManagement.Backend.Tests.Mqtt;

public class MqttNetRpcClientFacts
{
    private const int TestTimeout = 950;

    private IOptions<MqttNetRpcClientOptions> Options { get; }

    public MqttNetRpcClientFacts()
    {
        Options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
        Options.Value
            .Returns(new MqttNetRpcClientOptions() { RequestTimeout = Timeout.InfiniteTimeSpan, });
    }

    public class Request : MqttNetRpcClientFacts
    {
        [Fact]
        public async Task Throws_ArgumentException_if_request_topic_is_empty()
        {
            using MqttNetRpcClient client = new(Options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var act = client.Awaiting(_ => _.Request("  ", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("requestTopic").WithMessage("*topic*invalid*");
        }

        [Fact]
        public async Task Throws_ArgumentException_if_response_topic_is_empty()
        {
            using MqttNetRpcClient client = new(Options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var act = client.Awaiting(_ => _.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("responseTopic").WithMessage("*topic*invalid*");
        }

        [Fact]
        public async Task Throws_ArgumentException_if_method_is_empty()
        {
            using MqttNetRpcClient client = new(Options, Substitute.For<IMqttClient>());

            var act = client.Awaiting(_ =>
            {
                using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
                return _.Request("request", MqttRpcRequest.PlainText("", "Moritz"), "response", cancellationTokenSource.Token);
            });

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("request").WithMessage("*method*invalid*");
        }

        [Fact]
        public async Task Publishes_correct_message()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetRpcClient client = new(Options, mqttClient);

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            try
            {
                await client.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token);
            }
            catch { }

            await mqttClient.Received(1).PublishAsync(
                FluentArg.Assert<MqttApplicationMessage>(m =>
                {
                    m.UserProperties.Should().ContainSingle().Which.Should().BeEquivalentTo(new MqttUserProperty("Method", "greet"));
                    Encoding.UTF8.GetString(m.Payload).Should().BeEquivalentTo("Moritz");
                    m.ContentType.Should().Be(MediaTypeNames.Text.Plain);
                    m.QualityOfServiceLevel.Should().Be(MqttQualityOfServiceLevel.ExactlyOnce);
                    m.CorrelationData.Should().NotBeNull();
                    m.Retain.Should().BeFalse();
                }), Arg.Is(cancellationTokenSource.Token));
        }

        [Fact]
        public async Task Subscribes_and_unsubscribes_response_topic()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetRpcClient client = new(Options, mqttClient);

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            try
            {
                await client.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token);
            }
            catch { }

            List<MqttClientSubscribeOptions> subscriptions = [];
            await mqttClient.Received(1).SubscribeAsync(
                FluentArg.Assert<MqttClientSubscribeOptions>(o =>
                {
                    var subscription = o.TopicFilters.Should().ContainSingle().Subject;
                    subscription.Topic.Should().BeEquivalentTo("response");
                    subscription.QualityOfServiceLevel.Should().Be(MqttQualityOfServiceLevel.ExactlyOnce);
                }), Arg.Is(cancellationTokenSource.Token));
            await mqttClient.ReceivedWithAnyArgs(1).UnsubscribeAsync(default, default);
        }

        [Fact]
        public async Task Returns_content_of_response()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetRpcClient client = new(Options, mqttClient);
            mqttClient.When(x => x
                .PublishAsync(Arg.Is<MqttApplicationMessage>(m => m.Topic == "request"), Arg.Any<CancellationToken>())).Do(x => mqttClient
                .ReceiveApplicationMessage(x, (publish, receive) => receive
                    .WithTopic("response")
                    .WithPayload("Hello Moritz.")
                    .WithContentType(MediaTypeNames.Text.Plain)
                    .WithUserProperty("Status", "ok")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithCorrelationData(publish.CorrelationData)));

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var response = await client.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token);

            response.Status.Should().Be("ok");
            Encoding.UTF8.GetString(response.Payload!.Value).Should().BeEquivalentTo("Hello Moritz.");
            response.ContentType.Should().Be(MediaTypeNames.Text.Plain);
        }

        [Fact]
        public async Task Ignores_response_of_other_requests()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetRpcClient client = new(Options, mqttClient);
            mqttClient.When(x => x
                .PublishAsync(Arg.Is<MqttApplicationMessage>(m => m.Topic == "request"), Arg.Any<CancellationToken>())).Do(x => mqttClient
                .ReceiveApplicationMessage(x, (publish, receive) => receive
                    .WithTopic("response")
                    .WithPayload("Hello Moritz.")
                    .WithUserProperty("Status", "ok")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithCorrelationData([])));

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var act = client.Awaiting(_ => _.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token));

            await cancellationTokenSource.CancelAsync();
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_does_not_contain_status()
        {
            var mqttClient = Substitute.For<IMqttClient>();
            using MqttNetRpcClient client = new(Options, mqttClient);
            mqttClient.When(x => x
                .PublishAsync(Arg.Is<MqttApplicationMessage>(m => m.Topic == "request"), Arg.Any<CancellationToken>())).Do(x => mqttClient
                .ReceiveApplicationMessage(x, (publish, receive) => receive
                    .WithTopic("response")
                    .WithPayload("Hello Moritz.")
                    .WithUserProperty("", "")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithCorrelationData(publish.CorrelationData)));

            using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
            var act = client.Awaiting(_ => _.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*response*not*contain*status*");
        }

        [Fact]
        public async Task Throws_TaskCanceledException_if_request_has_a_timeout_as_configured()
        {
            var options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
            options.Value
                .Returns(new MqttNetRpcClientOptions() { RequestTimeout = TimeSpan.FromSeconds(1), });
            using MqttNetRpcClient client = new(options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var act = client.Awaiting(_ => _.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<TaskCanceledException>().WithMessage("*server*not*respond*00:00:01*");
            cancellationTokenSource.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public async Task Throws_TaskCanceledException_if_request_gets_cancelled_before_timeout()
        {
            var options = Substitute.For<IOptions<MqttNetRpcClientOptions>>();
            options.Value.Returns(new MqttNetRpcClientOptions() { RequestTimeout = TimeSpan.FromSeconds(2), });
            using MqttNetRpcClient client = new(options, Substitute.For<IMqttClient>());

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var act = client.Awaiting(_ => _.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token));

            await act.Should().ThrowAsync<TaskCanceledException>().WithMessage("*task*canceled*");
            cancellationTokenSource.IsCancellationRequested.Should().BeTrue();
        }
    }

    public class Dispose : MqttNetRpcClientFacts
    {
        [Fact]
        public async Task Cancels_pending_requests()
        {
            MqttNetRpcClient client = new(Options, Substitute.For<IMqttClient>());
            using CancellationTokenSource cancellationTokenSource = new();

            var requestTask = client.Request("request", MqttRpcRequest.PlainText("greet", "Moritz"), "response", cancellationTokenSource.Token);
            client.Dispose();

            var firstCompletedTask = await Task.WhenAny(requestTask, Task.Delay(TestTimeout, cancellationTokenSource.Token));
            await cancellationTokenSource.CancelAsync();
            firstCompletedTask.Should().BeSameAs(requestTask, "request should be cancelled due to Dispose()");
            await FluentActions.Awaiting(() => requestTask).Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
