using System.Net.Mime;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Extensions;
using ClusterManagement.Backend.Engines.Consumers;
using ClusterManagement.Backend.Tests.Mqtt;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public;
using MassTransit;
using MQTTnet;
using MQTTnet.Protocol;
using NSubstitute;
using Xunit;

namespace ClusterManagement.Backend.Tests.Engines.Consumers;

public class RetrieveFunctionBlockParametersConsumerFacts
{
    [Fact]
    public async Task Retrieves_all_configured_parameters()
    {
        // Arrange
        var mqttClient = Substitute.For<IMqttClient>();

        var functionBlock1Id = Guid.NewGuid();
        var settingId = Guid.NewGuid();
        object? settingValue = 123;
        var functionBlock2Id = Guid.NewGuid();
        var variableId = Guid.NewGuid();
        object? variableValue = "text";
        RetrieveFunctionBlockParametersRequest request = new(
        [
            new FunctionBlockParameterIds(functionBlock1Id, [settingId,]),
            new FunctionBlockParameterIds(functionBlock2Id, [variableId,]),
        ]);

        mqttClient.When(x => x
            .SubscribeAsync(Arg.Is<MqttClientSubscribeOptions>(o => o.GetTopic() == settingId.ToString()))).Do(x => mqttClient
            .ReceiveApplicationMessage(x => x
                .WithTopic(settingId.ToString())
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(settingValue))
                .WithPayloadFormatIndicator(MqttPayloadFormatIndicator.CharacterData)
                .WithContentType(MediaTypeNames.Application.Json)
                .WithUserProperty("Type", settingValue.GetType().FullName)));
        mqttClient.When(x => x
            .SubscribeAsync(Arg.Is<MqttClientSubscribeOptions>(o => o.GetTopic() == variableId.ToString()))).Do(x => mqttClient
            .ReceiveApplicationMessage(x => x
                .WithTopic(variableId.ToString())
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(variableValue))
                .WithPayloadFormatIndicator(MqttPayloadFormatIndicator.CharacterData)
                .WithContentType(MediaTypeNames.Application.Json)
                .WithUserProperty("Type", variableValue.GetType().FullName)));

        var consumeContext = Substitute.For<ConsumeContext<RetrieveFunctionBlockParametersRequest>>();
        consumeContext.Message.Returns(request);
        RetrieveFunctionBlockParametersResponse response = null!;
        await consumeContext.RespondAsync(Arg.Do<object>(o => response = (RetrieveFunctionBlockParametersResponse)o));
        RetrieveFunctionBlockParametersConsumer consumer = new(mqttClient);

        // Act
        consumer.Awaiting(_ => _.Consume(consumeContext)).ExecutionTime().Should().BeLessThan(400.Milliseconds());

        // Assert
        response.RequestError.Should().BeNull();
        response.FunctionBlockParameters.Should().BeEquivalentTo(new FunctionBlockParameterValues[]
        {
            new(functionBlock1Id, new Dictionary<Guid, FunctionBlockParameterValue>() { { settingId, new(settingValue.GetType().FullName!, "123") }, }),
            new(functionBlock2Id, new Dictionary<Guid, FunctionBlockParameterValue>() { { variableId, new(variableValue.GetType().FullName!, "\"text\"") }, }),
        });
    }

    [Fact]
    public async Task Waits_at_most_one_second_for_values()
    {
        // Arrange
        var mqttClient = Substitute.For<IMqttClient>();

        var functionBlockId = Guid.NewGuid();
        var settingId = Guid.NewGuid();
        object? settingValue = 123;
        RetrieveFunctionBlockParametersRequest request = new(
        [
            new FunctionBlockParameterIds(functionBlockId, [settingId,]),
        ]);

        var consumeContext = Substitute.For<ConsumeContext<RetrieveFunctionBlockParametersRequest>>();
        consumeContext.Message.Returns(request);
        RetrieveFunctionBlockParametersResponse response = null!;
        await consumeContext.RespondAsync(Arg.Do<object>(o => response = (RetrieveFunctionBlockParametersResponse)o));
        RetrieveFunctionBlockParametersConsumer consumer = new(mqttClient);

        // Act
        consumer.Awaiting(_ => _.Consume(consumeContext)).ExecutionTime().Should().BeCloseTo(1.Seconds(), 500.Milliseconds());

        // Assert
        response.RequestError.Should().BeNull();
        response.FunctionBlockParameters.Should().HaveCount(1);
    }

    [Fact]
    public async Task Returns_empty_values_for_unavailble_parameter()
    {
        // Arrange
        var mqttClient = Substitute.For<IMqttClient>();

        var functionBlockId = Guid.NewGuid();
        var settingId = Guid.NewGuid();
        object? settingValue = 123;
        RetrieveFunctionBlockParametersRequest request = new(
        [
            new FunctionBlockParameterIds(functionBlockId, [settingId,]),
        ]);

        var consumeContext = Substitute.For<ConsumeContext<RetrieveFunctionBlockParametersRequest>>();
        consumeContext.Message.Returns(request);
        RetrieveFunctionBlockParametersResponse response = null!;
        await consumeContext.RespondAsync(Arg.Do<object>(o => response = (RetrieveFunctionBlockParametersResponse)o));
        RetrieveFunctionBlockParametersConsumer consumer = new(mqttClient);

        // Act
        await consumer.Consume(consumeContext);

        // Assert
        response.RequestError.Should().BeNull();
        response.FunctionBlockParameters.Should().BeEquivalentTo(new FunctionBlockParameterValues[]
        {
            new(functionBlockId, new Dictionary<Guid, FunctionBlockParameterValue>()),
        });
    }

    [Fact]
    public async Task Handles_failures()
    {
        // Arrange
        var mqttClient = Substitute.For<IMqttClient>();
        InvalidOperationException exception = new("Could not subscribe.");
        mqttClient.WhenForAnyArgs(x => x.SubscribeAsync(default, default)).Throw(exception);

        var functionBlockId = Guid.NewGuid();
        var settingId = Guid.NewGuid();
        object? settingValue = 123;
        RetrieveFunctionBlockParametersRequest request = new(
        [
            new FunctionBlockParameterIds(functionBlockId, [settingId,]),
        ]);

        var consumeContext = Substitute.For<ConsumeContext<RetrieveFunctionBlockParametersRequest>>();
        consumeContext.Message.Returns(request);
        RetrieveFunctionBlockParametersResponse response = null!;
        await consumeContext.RespondAsync(Arg.Do<object>(o => response = (RetrieveFunctionBlockParametersResponse)o));
        RetrieveFunctionBlockParametersConsumer consumer = new(mqttClient);

        // Act
        await consumer.Consume(consumeContext);

        // Assert
        Assert.NotNull(response.RequestError);
        response.RequestError.Message.Should().Be(exception.Message);
        response.FunctionBlockParameters.Should().BeEmpty();
    }
}
