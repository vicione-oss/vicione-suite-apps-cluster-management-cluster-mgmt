using ClusterManagement.Backend.Iodds;
using AwesomeAssertions;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds;

public class IoddParser_ParseText
{
    [Fact]
    public void Parses_correctly()
    {
        var ioDevice = IoddParser.ParseText(IoddConstants.IoddDeviceTwo);

        ioDevice.DocumentInfo.Version.Should().Be("V2.4.1");
        ioDevice.DocumentInfo.ReleaseDate.Should().Be(new DateOnly(2012, 12, 10));
        ioDevice.DocumentInfo.Copyright.Should().Be("Copyright 2012, ifm electronic gmbh");
        ioDevice.ProfileHeader.ProfileRevision.Should().Be("1.00");
        ioDevice.DeviceIdentity.VendorId.Should().Be(310);
        ioDevice.DeviceIdentity.VendorName.Should().Be("ifm electronic gmbh");
        ioDevice.DeviceIdentity.VendorLogo.Should().Be("ifm-logo.png");
        ioDevice.DeviceIdentity.DeviceId.Should().Be(2);
        ioDevice.DeviceIdentity.DeviceName.Should().BeEmpty();
        ioDevice.DeviceIdentity.DeviceFamily.Should().Be("PP");
        ioDevice.DeviceIdentity.Variants.Should().HaveCount(2);
        ioDevice.DeviceIdentity.Variants.Should().SatisfyRespectively(
            first =>
            {
                first.ProductId.Should().Be("PP7550");
                first.HardwareRevision.Should().Be(string.Empty);
                first.FirmwareRevision.Should().Be(string.Empty);
                first.SymbolFile.Should().Be("ifm-PPAA-pic.png");
                first.IconFile.Should().Be("ifm-PPAA-icon.png");
                first.Name.Should().Be("PP7550");
                first.Description.Should().Be("PP7550 electronic pressure sensor, 0 to 400 bar");
            },
            second =>
            {
                second.ProductId.Should().Be("PP000E");
                second.HardwareRevision.Should().Be(string.Empty);
                second.FirmwareRevision.Should().Be(string.Empty);
                second.SymbolFile.Should().Be("ifm-PPAA-pic.png");
                second.IconFile.Should().Be("ifm-PPAA-icon.png");
                second.Name.Should().Be("PP000E");
                second.Description.Should().Be("PP000E electronic pressure sensor, 0 to 400 bar, E1 Certificate");
            });
    }

    [Fact]
    public void Can_handle_revision_1_1()
    {
        var ioDevice = IoddParser.ParseText(IoddConstants.IoddDeviceRevOneOne);

        ioDevice.ProfileHeader.ProfileRevision.Should().Be("1.1");
        ioDevice.DeviceIdentity.DeviceName.Should().Be("#171939");
        var variant = ioDevice.DeviceIdentity.Variants.Should().ContainSingle().Which;
        variant.Name.Should().Be("PV2304");
        variant.Description.Should().Be("Electronic pressure switch, -1.000...10.000 bar, IO-Link, cULus, G 1/4 A / M5 I nach ISO 1179-2");
    }

    [Fact]
    public void Can_handle_missing_values()
    {
        var ioDevice = IoddParser.ParseText(IoddConstants.IoddDeviceWithoutSomeValues);

        ioDevice.DocumentInfo.ReleaseDate.Should().Be(null);
        ioDevice.DocumentInfo.Copyright.Should().Be(string.Empty);
        ioDevice.DeviceIdentity.DeviceFamily.Should().Be(string.Empty);
        ioDevice.DeviceIdentity.Variants.Should().SatisfyRespectively(
            first =>
            {
                first.ProductId.Should().Be("PP7550");
                first.HardwareRevision.Should().Be(string.Empty);
                first.FirmwareRevision.Should().Be(string.Empty);
                first.SymbolFile.Should().Be(string.Empty);
                first.IconFile.Should().Be(string.Empty);
            },
            second =>
            {
                second.ProductId.Should().Be("PP000E");
                second.HardwareRevision.Should().Be(string.Empty);
                second.FirmwareRevision.Should().Be(string.Empty);
                second.SymbolFile.Should().Be(string.Empty);
                second.IconFile.Should().Be(string.Empty);
            });
    }
}
