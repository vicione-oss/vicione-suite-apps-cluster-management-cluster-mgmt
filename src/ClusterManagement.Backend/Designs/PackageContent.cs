using Sdk.Messaging;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

public record PackageContent(IReadOnlyDictionary<Guid, FunctionBlockDesign> Designs, IReadOnlyDictionary<string, Ruleset> Rulesets, bool Hide)
{
    public ErrorInfo? Error { get; init; }
}
