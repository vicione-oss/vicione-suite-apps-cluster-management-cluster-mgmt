using System.Net.Mime;
using System.Text;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Mqtt;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using ClusterManagement.Backend.Tests.TestUtils;

namespace ClusterManagement.Backend.Tests.Engines;

public class MqttEngineCommunicationFacts
{
    private readonly IMqttRpcClient _mqttClient;
    private readonly MqttEngineCommunication _communication;
    private readonly Guid _engineId = Guid.NewGuid();

    public MqttEngineCommunicationFacts()
    {
        _mqttClient = Substitute.For<IMqttRpcClient>();
        _communication = new(_mqttClient);
    }

    public class ChangeLogLevel : MqttEngineCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok"));

            await _communication.ChangeLogLevel(_engineId, LogLevel.Warning, CancellationToken.None);

            await _mqttClient.Received(1).Request(Arg.Is($"{_engineId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("changeloglevel");
                    Encoding.UTF8.GetString(r.Payload!.Value).Should().Be("""{"LogLevel":3}""");
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", new(Encoding.UTF8.GetBytes("not possible")), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.ChangeLogLevel(_engineId, LogLevel.Critical, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*changeloglevel*not possible*");
        }
    }

    public class LoadSettings : MqttEngineCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok"));
            var settingsId = Guid.NewGuid();

            await _communication.LoadSettings(_engineId, new DateTime(23, 2, 6, 5, 4, 3),
                [new(settingsId, new("int", "5")),], CancellationToken.None);

            await _mqttClient.Received(1).Request(Arg.Is($"{_engineId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("loadsettings");
                    Encoding.UTF8.GetString(r.Payload!.Value).Should().Be($$$"""{"Entries":[{"UniqueIdentifier":"{{{settingsId}}}","Time":"0023-02-06T05:04:03","Value":{"Type":"int","Value":"5"}}]}""");
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", new(Encoding.UTF8.GetBytes("not possible")), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.LoadSettings(_engineId, DateTime.MinValue, [], CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*loadsettings*not possible*");
        }
    }

    public class LoadPersistentVariables : MqttEngineCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok"));
            var variableId = Guid.NewGuid();

            await _communication.LoadPersistentVariables(_engineId, new DateTime(23, 2, 6, 5, 4, 3),
                [new(variableId, new("int", "5")),], CancellationToken.None);

            await _mqttClient.Received(1).Request(Arg.Is($"{_engineId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("loadvariables");
                    Encoding.UTF8.GetString(r.Payload!.Value).Should().Be($$$"""{"Entries":[{"UniqueIdentifier":"{{{variableId}}}","Time":"0023-02-06T05:04:03","Value":{"Type":"int","Value":"5"}}]}""");
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", new(Encoding.UTF8.GetBytes("not possible")), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.LoadPersistentVariables(_engineId, DateTime.MinValue, [], CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*loadvariables*not possible*");
        }
    }
}
