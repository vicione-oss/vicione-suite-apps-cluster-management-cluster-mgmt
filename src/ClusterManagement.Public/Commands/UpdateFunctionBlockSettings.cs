using Sdk.Messaging;

namespace ClusterManagement.Public.Commands;

public sealed record UpdateFunctionBlockSettings(Guid ClusterId, List<FunctionBlockParameterValues> SettingsToApply) : ICommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
