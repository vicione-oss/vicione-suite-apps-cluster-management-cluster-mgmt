using ClusterManagement.Internal.Commands;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

[ReadOnlyConsumer]
public sealed class ApplyFunctionBlockSettingsConsumer(IEngineCommunication engineCommunication) : IConsumer<ApplyFunctionBlockSettings>
{
    private readonly IEngineCommunication _engineCommunication = engineCommunication;

    public async Task Consume(ConsumeContext<ApplyFunctionBlockSettings> context)
    {
        var time = DateTime.UtcNow;
        var settings = context.Message.SettingsToApply.SelectMany(s => s.ParametersValue.Select(v => new PersistenceEntry(v.Key, new(v.Value.Type, v.Value.Value)))).ToList();
        await _engineCommunication.LoadSettings(context.Message.EngineId, time, settings, context.CancellationToken);
    }
}
