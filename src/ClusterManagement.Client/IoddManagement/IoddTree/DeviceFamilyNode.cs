using ClusterManagement.Client.IoddManagement.Models;
using ViciOne.Ui.TreeEditor.Builder.Interface;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal class DeviceFamilyNode : ITreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required VendorNode VendorNode { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }
    public bool Selected { get; set; }

    public List<DeviceView> Devices { get; set; } = [];
}
