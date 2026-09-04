namespace ClusterManagement.Backend.Engines;

internal sealed class MqttCommunicationComparer : NullableEqualityComparer<MqttCommunication>
{
    private protected override bool CoreEquals(MqttCommunication x, MqttCommunication y)
        => StringComparer.Ordinal.Equals(x.Address, y.Address) &&
        StringComparer.Ordinal.Equals(x.Password, y.Password) &&
        EqualityComparer<MqttProtocol>.Default.Equals(x.Protocol, y.Protocol) &&
        EqualityComparer<int?>.Default.Equals(x.TcpPort, y.TcpPort) &&
        StringComparer.Ordinal.Equals(x.Username, y.Username) &&
        EqualityComparer<short>.Default.Equals(x.QualityOfService, y.QualityOfService) &&
        EqualityComparer<bool>.Default.Equals(x.Retain, y.Retain);

    public override int GetHashCode(MqttCommunication obj)
    {
        HashCode hash = new();
        hash.Add(obj.Address, StringComparer.Ordinal);
        hash.Add(obj.Password, StringComparer.Ordinal);
        hash.Add(obj.Protocol);
        hash.Add(obj.TcpPort);
        hash.Add(obj.Username, StringComparer.Ordinal);
        hash.Add(obj.QualityOfService);
        hash.Add(obj.Retain);
        return hash.ToHashCode();
    }
}
