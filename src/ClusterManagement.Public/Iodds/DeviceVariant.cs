namespace ClusterManagement.Public.Iodds;

public class DeviceVariant
{
    public required string ProductId { get; init; }
    public required string HardwareRevision { get; init; }
    public required string FirmwareRevision { get; init; }
    public required string SymbolFile { get; init; }
    public required string IconFile { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
}
