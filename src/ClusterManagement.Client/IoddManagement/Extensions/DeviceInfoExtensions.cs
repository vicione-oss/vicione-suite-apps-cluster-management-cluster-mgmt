using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Client.IoddManagement.Extensions;

internal static class DeviceInfoExtensions
{
    internal static IEnumerable<DeviceView> CreateDeviceVariants(this DeviceInfo deviceInfo)
        => deviceInfo.DeviceDescription.DeviceIdentity.Variants.Select(deviceInfo.Create);

    internal static DeviceView Create(this DeviceInfo deviceInfo, DeviceVariant deviceVariant)
        => new()
        {
            DeviceDescription = deviceVariant.Description,
            DeviceId = deviceInfo.DeviceDescription.DeviceIdentity.DeviceId,
            DeviceFamily = GetDeviceFamily(deviceInfo.DeviceDescription.DeviceIdentity.DeviceFamily),
            DeviceImage = deviceVariant.SymbolFile,
            Filename = deviceInfo.Filename,
            FirmwareRevision = deviceVariant.FirmwareRevision,
            HardwareRevision = deviceVariant.HardwareRevision,
            IconFile = deviceVariant.IconFile,
            IoddReleaseDate = deviceInfo.DeviceDescription.DocumentInfo.ReleaseDate,
            IoLinkRevision = deviceInfo.DeviceDescription.ProfileHeader.ProfileRevision,
            Name = deviceVariant.Name,
            ProductId = deviceVariant.ProductId,
            UploadedAt = deviceInfo.LastWriteTime,
            VendorName = GetVendorName(deviceInfo.DeviceDescription.DeviceIdentity),
            Version = deviceInfo.DeviceDescription.DocumentInfo.Version,
            VendorId = deviceInfo.DeviceDescription.DeviceIdentity.VendorId,
        };

    private static string GetVendorName(DeviceIdentity deviceIdentity)
        => deviceIdentity.VendorId == 310 ? IfmSpecific.s_vendorName : deviceIdentity.VendorName;

    private static string GetDeviceFamily(string deviceFamily)
        => IfmSpecific.CategoryNames.TryGetValue(deviceFamily, out var name) ? name : deviceFamily;
}
