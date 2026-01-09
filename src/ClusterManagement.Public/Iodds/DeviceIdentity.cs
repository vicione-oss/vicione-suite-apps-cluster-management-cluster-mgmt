namespace ClusterManagement.Public.Iodds;

public class DeviceIdentity
{
    public required int VendorId { get; init; }
    public required string VendorName { get; init; }
    public required string VendorLogo { get; init; }
    public required int DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string DeviceFamily { get; init; }
    public required IReadOnlyCollection<DeviceVariant> Variants { get; init; }
}
