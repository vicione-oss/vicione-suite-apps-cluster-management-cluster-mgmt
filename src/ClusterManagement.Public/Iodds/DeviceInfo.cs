namespace ClusterManagement.Public.Iodds;

public sealed class DeviceInfo
{
    public required IODevice DeviceDescription { get; init; }
    public required string Filename { get; init; }
    public required DateTimeOffset LastWriteTime { get; init; }
}
