using ClusterManagement.Backend.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines;

internal sealed class FunctionBlockComparer : NullableEqualityComparer<FunctionBlock>
{
    private protected override bool CoreEquals(FunctionBlock x, FunctionBlock y) =>
        x.Connectors.OrderBy(x => x.Id).SequenceEqual(y.Connectors.OrderBy(x => x.Id), new FunctionBlockConnectorComparer()) &&
        EqualityComparer<Guid?>.Default.Equals(x.ContainerId, y.ContainerId) &&
        EqualityComparer<uint>.Default.Equals(x.CycleFrequency, y.CycleFrequency) &&
        EqualityComparer<Guid>.Default.Equals(x.DesignId, y.DesignId) &&
        StringComparer.Ordinal.Equals(x.Name, y.Name) &&
        EqualityComparer<uint>.Default.Equals(x.RunIndex, y.RunIndex) &&
        EqualityComparer<FunctionBlockRunMode>.Default.Equals(x.RunMode, y.RunMode) &&
        x.Settings.OrderBy(x => x.Id).SequenceEqual(y.Settings.OrderBy(x => x.Id), new FunctionBlockSettingComparer()) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id) &&
        x.Variables.OrderBy(x => x.Id).SequenceEqual(y.Variables.OrderBy(x => x.Id), new FunctionBlockVariableComparer());

    public override int GetHashCode(FunctionBlock obj)
    {
        HashCode hash = new();
        hash.AddRange(obj.Connectors.OrderBy(x => x.Id), new FunctionBlockConnectorComparer());
        hash.Add(obj.ContainerId);
        hash.Add(obj.CycleFrequency);
        hash.Add(obj.DesignId);
        hash.Add(obj.Name, StringComparer.Ordinal);
        hash.Add(obj.RunIndex);
        hash.Add(obj.RunMode);
        hash.AddRange(obj.Settings.OrderBy(x => x.Id), new FunctionBlockSettingComparer());
        hash.Add(obj.Id);
        hash.AddRange(obj.Variables.OrderBy(x => x.Id), new FunctionBlockVariableComparer());
        return hash.ToHashCode();
    }
}
