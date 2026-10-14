using System.Globalization;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Client.IoddManagement.Extensions;

internal static class DeviceInfoExtensions
{
    internal static IEnumerable<DeviceView> CreateDeviceVariants(this DeviceInfo deviceInfo)
        => deviceInfo.DeviceDescription.DeviceIdentity.Variants.Select(deviceInfo.Create);

    /// <summary>
    /// Names the device by its vendor and its first variant as listed in the IODD tree, followed by "…" if it has more,
    /// e.g. <c>ifm electronic O5D100…</c>. An IODD can list dozens of variants, which would make a message too long.
    /// </summary>
    internal static string GetDisplayName(this DeviceInfo deviceInfo)
    {
        var deviceIdentity = deviceInfo.DeviceDescription.DeviceIdentity;
        var variantNames = deviceIdentity.Variants.Select(v => v.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(2).ToList();
        var deviceName = variantNames.Count switch
        {
            0 => null,
            1 => variantNames[0],
            _ => $"{variantNames[0]}…",
        };

        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = deviceIdentity.DeviceName;
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = deviceIdentity.DeviceId.ToString(CultureInfo.InvariantCulture);

        return $"{GetVendorName(deviceIdentity)} {deviceName}";
    }

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
