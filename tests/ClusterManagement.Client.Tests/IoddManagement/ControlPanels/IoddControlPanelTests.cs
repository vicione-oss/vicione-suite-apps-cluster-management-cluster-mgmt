using Bunit;
using ClusterManagement.Client.IoddManagement.ControlPanels;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using Sdk.Client.Contracts;
using Sdk.Client.Services;

namespace ClusterManagement.Client.Tests.IoddManagement.ControlPanels;

public class IoddControlPanelTests
{
    [Fact]
    public async Task Shows_the_description_of_a_device_variant_when_its_name_is_hovered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupIoddControlPanel(Substitute.For<IIoddService>(), Substitute.For<IMessageBannerService>(),
            Substitute.For<IUploadTicketFactory>(), Substitute.For<IStreamUploadHandler<IoddUpload>>());
        var state = new IoddControlPanelState { Devices = [Device(1, "O5D150", "Photoelectric distance sensor"), Device(2, "KG5065", "")] };

        // Act
        var sut = ctx.Render<IoddControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        sut.WaitForAssertion(() => sut.FindAll(".iodds-grid span[title]").Select(name => name.GetAttribute("title"))
            .Should().Equal("KG5065", "O5D150\nPhotoelectric distance sensor"));

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    private static DeviceView Device(int deviceId, string name, string description)
        => new()
        {
            DeviceDescription = description,
            DeviceFamily = "Sensors",
            DeviceId = deviceId,
            DeviceImage = string.Empty,
            Filename = string.Empty,
            FirmwareRevision = string.Empty,
            HardwareRevision = string.Empty,
            IconFile = string.Empty,
            IoLinkRevision = string.Empty,
            Name = name,
            ProductId = $"P{deviceId}",
            VendorId = 310,
            VendorName = "ifm",
            Version = string.Empty,
        };
}
