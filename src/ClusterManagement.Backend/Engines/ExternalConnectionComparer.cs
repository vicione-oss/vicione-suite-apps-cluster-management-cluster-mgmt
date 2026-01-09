using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class ExternalConnectionComparer : NullableEqualityComparer<ExternalConnection>
{
    private protected override bool CoreEquals(ExternalConnection x, ExternalConnection y) =>
        StringComparer.Ordinal.Equals(x.Name, y.Name) &&
        new CommunicationConfigurationComparer().Equals(x.Communication, y.Communication) &&
        x.IncomingLinks.OrderBy(x => x.ConnectorId).SequenceEqual(y.IncomingLinks.OrderBy(x => x.ConnectorId), new IncomingLinkComparer()) &&
        x.OutgoingLinks.OrderBy(x => x.ConnectorId).SequenceEqual(y.OutgoingLinks.OrderBy(x => x.ConnectorId), new OutgoingLinkComparer());

    public override int GetHashCode(ExternalConnection obj)
    {
        HashCode hash = new();
        hash.Add(obj.Name, StringComparer.Ordinal);
        hash.Add(obj.Communication, new CommunicationConfigurationComparer());
        hash.AddRange(obj.IncomingLinks.OrderBy(x => x.ConnectorId), new IncomingLinkComparer());
        hash.AddRange(obj.OutgoingLinks.OrderBy(x => x.ConnectorId), new OutgoingLinkComparer());
        return hash.ToHashCode();
    }
}
