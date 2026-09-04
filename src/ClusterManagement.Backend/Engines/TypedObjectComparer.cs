namespace ClusterManagement.Backend.Engines;

internal sealed class TypedObjectComparer : NullableEqualityComparer<TypedObject>
{
    private protected override bool CoreEquals(TypedObject x, TypedObject y)
        => StringComparer.Ordinal.Equals(x.Type, y.Type) &&
        StringComparer.Ordinal.Equals(x.Value, y.Value);

    public override int GetHashCode(TypedObject obj)
    {
        HashCode hash = new();
        hash.Add(obj.Type, StringComparer.Ordinal);
        hash.Add(obj.Value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
