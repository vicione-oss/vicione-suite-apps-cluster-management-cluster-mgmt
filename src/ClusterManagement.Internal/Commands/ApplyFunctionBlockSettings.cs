using ClusterManagement.Public;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

public sealed record ApplyFunctionBlockSettings(Guid EngineId, List<FunctionBlockParameterValues> SettingsToApply) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
