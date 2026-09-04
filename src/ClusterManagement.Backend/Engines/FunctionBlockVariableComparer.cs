using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class FunctionBlockVariableComparer : NullableEqualityComparer<FunctionBlockVariable>
{
    private protected override bool CoreEquals(FunctionBlockVariable x, FunctionBlockVariable y)
        => EqualityComparer<Guid>.Default.Equals(x.DesignId, y.DesignId) &&
        EqualityComparer<DateTime>.Default.Equals(x.Timestamp, y.Timestamp) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id) &&
        new TypedObjectComparer().Equals(x.Value, y.Value);

    public override int GetHashCode(FunctionBlockVariable obj)
    {
        HashCode hash = new();
        hash.Add(obj.DesignId);
        hash.Add(obj.Timestamp);
        hash.Add(obj.Id);
        hash.AddNullable(obj.Value, new TypedObjectComparer());
        return hash.ToHashCode();
    }
}
