using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Client.Tests.IoddManagement;

internal static class DeviceInfoFactory
{
    /// <summary>
    /// Creates the device <c>Acme</c> <c>Sensor</c> with the given variant names, by default <c>Sensor A</c>, <c>Sensor B</c> and <c>Sensor A</c> again.
    /// </summary>
    public static DeviceInfo CreateDeviceInfo(int vendorId = 888, int deviceId = 7, string[]? variantNames = null)
        => new()
        {
            DeviceDescription = new IODevice
            {
                ProfileHeader = new() { ProfileRevision = "1.1" },
                DocumentInfo = new() { ReleaseDate = null, Copyright = string.Empty, Version = "V1.0" },
                DeviceIdentity = new()
                {
                    VendorId = vendorId,
                    VendorName = "Acme",
                    VendorLogo = string.Empty,
                    DeviceId = deviceId,
                    DeviceName = "Sensor",
                    DeviceFamily = string.Empty,
                    Variants = [.. (variantNames ?? ["Sensor A", "Sensor B", "Sensor A"]).Select(CreateVariant)],
                },
            },
            Filename = "Acme-Sensor-20250323-IODD1.1.xml",
            LastWriteTime = DateTimeOffset.UtcNow,
        };

    private static DeviceVariant CreateVariant(string name)
        => new()
        {
            ProductId = name,
            HardwareRevision = string.Empty,
            FirmwareRevision = string.Empty,
            SymbolFile = string.Empty,
            IconFile = string.Empty,
            Name = name,
            Description = string.Empty,
        };
}
