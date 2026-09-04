namespace ClusterManagement.Backend.Engines;

internal sealed class ContainerComparer : NullableEqualityComparer<Container>
{
    private protected override bool CoreEquals(Container x, Container y)
        => StringComparer.Ordinal.Equals(x.Name, y.Name) &&
        EqualityComparer<Guid?>.Default.Equals(x.ParentContainerId, y.ParentContainerId) &&
        EqualityComparer<Guid>.Default.Equals(x.Id, y.Id);

    public override int GetHashCode(Container obj)
    {
        HashCode hash = new();
        hash.Add(obj.Name, StringComparer.Ordinal);
        hash.Add(obj.ParentContainerId);
        hash.Add(obj.Id);
        return hash.ToHashCode();
    }
}
