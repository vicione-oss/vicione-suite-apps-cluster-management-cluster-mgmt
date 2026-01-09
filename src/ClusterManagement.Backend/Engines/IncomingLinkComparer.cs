using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class IncomingLinkComparer : NullableEqualityComparer<IncomingLink>
{
    private protected override bool CoreEquals(IncomingLink x, IncomingLink y) =>
        EqualityComparer<Guid>.Default.Equals(x.ConnectorId, y.ConnectorId) &&
        EqualityComparer<Guid>.Default.Equals(x.RemoteId, y.RemoteId) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.SourceType, y.SourceType) &&
        new TypedObjectComparer().Equals(x.TypeConverter, y.TypeConverter);

    public override int GetHashCode(IncomingLink obj)
    {
        HashCode hash = new();
        hash.Add(obj.ConnectorId);
        hash.Add(obj.RemoteId);
        hash.Add(obj.SourceType, StringComparer.Ordinal);
        hash.AddNullable(obj.TypeConverter, new TypedObjectComparer());
        return hash.ToHashCode();
    }
}
