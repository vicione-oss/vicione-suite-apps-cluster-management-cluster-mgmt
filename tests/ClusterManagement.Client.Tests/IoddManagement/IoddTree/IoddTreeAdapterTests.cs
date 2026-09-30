using ClusterManagement.Client.IoddManagement;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Models;
using ViciOne.Ui.MonochromeIcons.Assets.Services;
using ViciOne.Ui.TreeEditor.Builder;

namespace ClusterManagement.Client.Tests.IoddManagement.IoddTree;

public sealed class IoddTreeAdapterTests : IDisposable
{
    private readonly TreeBuilder _treeBuilder = new();
    private readonly IoddTreeAdapter _adapter = new(Substitute.For<IMonochromeIconSvgMarkupProvider>());

    public IoddTreeAdapterTests()
    {
        _treeBuilder.SetAdapter(_adapter);
        _adapter.CoupleTreeBuilderEvents();
    }

    public void Dispose()
    {
        _adapter.Dispose();
        _treeBuilder.Dispose();
    }

    private static DeviceView CreateDevice(int vendorId, string vendorName, int deviceId, string deviceFamily)
        => new()
        {
            DeviceDescription = string.Empty,
            DeviceFamily = deviceFamily,
            DeviceId = deviceId,
            DeviceImage = string.Empty,
            Filename = string.Empty,
            FirmwareRevision = string.Empty,
            HardwareRevision = string.Empty,
            IconFile = string.Empty,
            IoLinkRevision = string.Empty,
            Name = $"Device {deviceId}",
            ProductId = $"P{deviceId}",
            VendorId = vendorId,
            VendorName = vendorName,
            Version = string.Empty,
        };

    [Fact]
    public void Default_selection_should_be_the_repository_root_when_devices_exist()
    {
        // Arrange
        _adapter.AddDevice(CreateDevice(310, "ifm", 1, "Sensors"), false);
        _adapter.AddDevice(CreateDevice(310, "ifm", 2, "Actuators"), false);
        _adapter.AddDevice(CreateDevice(26, "Balluff", 3, "Sensors"), false);

        // Act
        var key = _adapter.GetDefaultSelectioNode();

        // Assert
        key.Should().Be(new TreeSelectionKey(0, null, null));
    }

    [Fact]
    public void Default_selection_should_be_the_repository_root_without_devices()
    {
        // Act
        var key = _adapter.GetDefaultSelectioNode();

        // Assert
        key.Should().Be(new TreeSelectionKey(0, null, null));
    }

    [Fact]
    public void Selecting_the_default_node_should_list_all_devices()
    {
        // Arrange
        DeviceView[] devices =
        [
            CreateDevice(310, "ifm", 1, "Sensors"),
            CreateDevice(310, "ifm", 2, "Actuators"),
            CreateDevice(26, "Balluff", 3, "Sensors"),
        ];

        foreach (var device in devices)
            _adapter.AddDevice(device, false);

        IEnumerable<DeviceView>? listed = null;
        _adapter.SelectionChanged += selection => listed = selection;

        // Act
        _adapter.SetSelection(_adapter.GetDefaultSelectioNode());

        // Assert
        listed.Should().BeEquivalentTo(devices);
        _adapter.SelectedNodeKey.Should().Be(new TreeSelectionKey(0, null, null));
    }
}
