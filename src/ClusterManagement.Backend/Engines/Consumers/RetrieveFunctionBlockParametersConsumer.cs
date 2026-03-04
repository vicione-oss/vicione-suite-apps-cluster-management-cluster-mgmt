using System.Collections.Concurrent;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public;
using MassTransit;
using MQTTnet;
using MQTTnet.Protocol;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class RetrieveFunctionBlockParametersConsumer(IMqttBrokerConnectionCache connectionProvider)
    : InstanceDependentRequestConsumer<RetrieveFunctionBlockParametersRequest, RetrieveFunctionBlockParametersResponse>
{
    private readonly IMqttClient? _mqttClient;

    internal RetrieveFunctionBlockParametersConsumer(IMqttClient? mqttClient = default)
        : this(default(IMqttBrokerConnectionCache)!)
        => _mqttClient = mqttClient;

    protected override async Task<RetrieveFunctionBlockParametersResponse> Respond(ConsumeContext<RetrieveFunctionBlockParametersRequest> context)
    {
        var mqttClient = await connectionProvider.ConnectClient(_mqttClient, default, context.CancellationToken);

        var parametersId = context.Message.FunctionBlockParameters.SelectMany(s => s.ParametersId).Select(id => id.ToString()).ToList();
        using CancellationTokenSource receiving = new();
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(receiving.Token, context.CancellationToken);

        ConcurrentDictionary<Guid, FunctionBlockParameterValue> receivedParametersValue = new();
        mqttClient.ApplicationMessageReceivedAsync += OnMessage;

        try
        {
            var subscribeTasks = parametersId.Select(id => mqttClient.SubscribeAsync(id, MqttQualityOfServiceLevel.AtLeastOnce)).ToArray();
            await Task.WhenAll(subscribeTasks).ConfigureAwait(false);

            try
            {
                await Task.Delay(1_000, waiting.Token).ConfigureAwait(false);
            }
            catch { }

            var unsubscribeTasks = parametersId.Select(id => mqttClient.UnsubscribeAsync(id)).ToArray();
            await Task.WhenAll(unsubscribeTasks).ConfigureAwait(false);
        }
        finally
        {
            mqttClient.ApplicationMessageReceivedAsync -= OnMessage;
            await mqttClient.DisconnectClient(_mqttClient, default, context.CancellationToken);
        }

        async Task OnMessage(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            var uniqueIdentifier = eventArgs.ApplicationMessage.Topic;
            var type = eventArgs.ApplicationMessage.UserProperties.Find(p => p.Name == "Type")?.Value
                ?? throw new InvalidOperationException($"MQTT user property 'Type' could not be found.");
            var value = (eventArgs.ApplicationMessage.Payload.Length > 0) ? eventArgs.ApplicationMessage.ConvertPayloadToString() : default;
            receivedParametersValue[Guid.Parse(eventArgs.ApplicationMessage.Topic)] = new(type, value);
            if (receivedParametersValue.Count == parametersId.Count)
                await receiving.CancelAsync();
        }

        List<FunctionBlockParameterValues> functionBlockParameters = [];
        foreach (var requestedFunctionBlock in context.Message.FunctionBlockParameters)
        {
            Dictionary<Guid, FunctionBlockParameterValue> parametersValue = [];
            foreach (var requestedParameterId in requestedFunctionBlock.ParametersId)
            {
                if (receivedParametersValue.TryGetValue(requestedParameterId, out var value))
                    parametersValue.Add(requestedParameterId, value);
            }
            functionBlockParameters.Add(new(requestedFunctionBlock.FunctionBlockId, parametersValue));
        }
        return new RetrieveFunctionBlockParametersResponse(functionBlockParameters);
    }

    protected override Task<RetrieveFunctionBlockParametersResponse> HandleException(ConsumeContext<RetrieveFunctionBlockParametersRequest> context, Exception e)
        => Task.FromResult(new RetrieveFunctionBlockParametersResponse([]) { RequestError = new(e.HResult, e.Message), });
}
