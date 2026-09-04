using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class FunctionBlockConnectorLinkComparer : NullableEqualityComparer<FunctionBlockConnectorLink>
{
    private protected override bool CoreEquals(FunctionBlockConnectorLink x, FunctionBlockConnectorLink y)
        => EqualityComparer<Guid>.Default.Equals(x.DestinationConnectorId, y.DestinationConnectorId) &&
        EqualityComparer<Guid>.Default.Equals(x.SourceConnectorId, y.SourceConnectorId) &&
        new TypedObjectComparer().Equals(x.TypeConverter, y.TypeConverter) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id);

    public override int GetHashCode(FunctionBlockConnectorLink obj)
    {
        HashCode hash = new();
        hash.Add(obj.DestinationConnectorId);
        hash.Add(obj.SourceConnectorId);
        hash.AddNullable(obj.TypeConverter, new TypedObjectComparer());
        hash.Add(obj.Id);
        return hash.ToHashCode();
    }
}
