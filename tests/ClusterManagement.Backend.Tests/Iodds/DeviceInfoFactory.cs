using ClusterManagement.Backend.Iodds;
using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Backend.Tests.Iodds;

internal static class DeviceInfoFactory
{
    public static DeviceInfo CreateDeviceInfo(int vendorId = 310, int deviceId = 2, string? filename = null)
    {
        IODevice? deviceDescription = null;

        if (vendorId == 310 && deviceId == 2)
        {
            deviceDescription = IoddParser.ParseStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(IoddConstants.IoddDeviceTwo)));
        }
        else if (vendorId == 311 && deviceId == 3)
        {
            deviceDescription = IoddParser.ParseStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(IoddConstants.IoddDeviceThree)));
        }
        else if (vendorId == 312 && deviceId == 4)
        {
            deviceDescription = IoddParser.ParseStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(IoddConstants.IoddDeviceRevOneOne)));
        }

        return new DeviceInfo
        {
            DeviceDescription = deviceDescription ?? throw new InvalidOperationException("Test"),
            Filename = filename ?? $"device-{vendorId}-{deviceId}-20250323-IODD1.1.xml",
            LastWriteTime = DateTime.UtcNow
        };
    }
}
