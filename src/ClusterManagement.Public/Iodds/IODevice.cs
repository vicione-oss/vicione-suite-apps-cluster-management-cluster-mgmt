namespace ClusterManagement.Public.Iodds;

public sealed class IODevice
{
    public required ProfileHeader ProfileHeader { get; init; }
    public required DocumentInfo DocumentInfo { get; init; }
    public required DeviceIdentity DeviceIdentity { get; init; }
}
