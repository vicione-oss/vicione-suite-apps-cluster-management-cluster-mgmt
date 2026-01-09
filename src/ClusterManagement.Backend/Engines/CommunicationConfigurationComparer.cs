using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

internal sealed class CommunicationConfigurationComparer : NullableEqualityComparer<CommunicationConfiguration>
{
    private protected override bool CoreEquals(CommunicationConfiguration x, CommunicationConfiguration y) =>
        StringComparer.Ordinal.Equals(x.Id, y.Id) &&
        x.Properties.OrderBy(x => x.Id).SequenceEqual(y.Properties.OrderBy(x => x.Id), new CommunicationPropertyComparer());

    public override int GetHashCode(CommunicationConfiguration obj)
    {
        HashCode hash = new();
        hash.Add(obj.Id, StringComparer.Ordinal);
        hash.AddRange(obj.Properties.OrderBy(x => x.Id), new CommunicationPropertyComparer());
        return hash.ToHashCode();
    }
}
