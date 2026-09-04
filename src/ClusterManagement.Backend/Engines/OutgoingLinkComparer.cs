namespace ClusterManagement.Backend.Engines;

internal sealed class OutgoingLinkComparer : NullableEqualityComparer<OutgoingLink>
{
    private protected override bool CoreEquals(OutgoingLink x, OutgoingLink y)
        => EqualityComparer<Guid>.Default.Equals(x.ConnectorId, y.ConnectorId) &&
        EqualityComparer<Guid>.Default.Equals(x.RemoteId, y.RemoteId);

    public override int GetHashCode(OutgoingLink obj)
    {
        HashCode hash = new();
        hash.Add(obj.ConnectorId);
        hash.Add(obj.RemoteId);
        return hash.ToHashCode();
    }
}
