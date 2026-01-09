using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class CommunicationPropertyComparer : NullableEqualityComparer<CommunicationProperty>
{
    private protected override bool CoreEquals(CommunicationProperty x, CommunicationProperty y) =>
        StringComparer.Ordinal.Equals(x.Id, y.Id) &&
        new TypedObjectComparer().Equals(x.Value, y.Value);

    public override int GetHashCode(CommunicationProperty obj)
    {
        HashCode hash = new();
        hash.Add(obj.Id, StringComparer.Ordinal);
        hash.AddNullable(obj.Value, new TypedObjectComparer());
        return hash.ToHashCode();
    }
}
