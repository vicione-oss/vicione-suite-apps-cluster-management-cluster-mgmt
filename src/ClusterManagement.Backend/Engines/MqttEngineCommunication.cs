using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Mqtt;
using Microsoft.Extensions.Logging;
using ViciOne.ManagedEngine.Pipelines.Commands;
using EnginePersistenceEntry = ViciOne.ManagedEngine.Persistence.PersistenceEntry;

namespace ClusterManagement.Backend.Engines;

internal sealed class MqttEngineCommunication(IMqttRpcClient mqttClient) : IEngineCommunication
{
    public async Task ChangeLogLevel(Guid engineId, LogLevel logLevel, CancellationToken cancellationToken)
    {
        var command = new ChangeLogLevelCommand() { LogLevel = logLevel, };
        var mqttRequest = MqttRpcRequest.Json("changeloglevel", command);
        _ = await mqttClient.RequestFromWorker(mqttRequest, engineId, cancellationToken);
    }

    public async Task LoadSettings(Guid engineId, DateTime timestamp, IReadOnlyCollection<PersistenceEntry> settings,
        CancellationToken cancellationToken)
    {
        var command = new LoadPersistenceSettingsCommand()
        {
            Entries = [.. settings.Select(e => new EnginePersistenceEntry()
            {
                Time = timestamp,
                UniqueIdentifier = e.Id.ToString(),
                Value = e.Value.ToJsonNode(),
            })],
        };
        var mqttRequest = MqttRpcRequest.Json("loadsettings", command, JsonSetup.DefaultOptions);
        _ = await mqttClient.RequestFromWorker(mqttRequest, engineId, cancellationToken);
    }

    public async Task LoadPersistentVariables(Guid engineId, DateTime timestamp, IReadOnlyCollection<PersistenceEntry> variables,
        CancellationToken cancellationToken)
    {
        var command = new LoadPersistenceVariablesCommand()
        {
            Entries = [.. variables.Select(e => new EnginePersistenceEntry()
            {
                Time = timestamp,
                UniqueIdentifier = e.Id.ToString(),
                Value = e.Value.ToJsonNode(),
            })],
        };
        var mqttRequest = MqttRpcRequest.Json("loadvariables", command, JsonSetup.DefaultOptions);
        _ = await mqttClient.RequestFromWorker(mqttRequest, engineId, cancellationToken);
    }
}
