namespace ClusterManagement.Client.IoddManagement.Models;

public class DeviceView
{
    public required string DeviceDescription { get; set; }
    public required string DeviceFamily { get; set; }
    public int DeviceId { get; set; }
    public required string Filename { get; set; }
    public required string FirmwareRevision { get; set; }
    public required string HardwareRevision { get; set; }
    public required string IconFile { get; set; }
    public DateOnly? IoddReleaseDate { get; set; }
    public required string IoLinkRevision { get; set; }
    public required string Name { get; set; }
    public required string ProductId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int VendorId { get; set; }
    public required string VendorName { get; set; }
    public required string Version { get; set; }
    public required string DeviceImage { get; set; }
}
