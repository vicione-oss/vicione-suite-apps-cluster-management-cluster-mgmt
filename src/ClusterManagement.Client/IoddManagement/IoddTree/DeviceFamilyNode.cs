using ClusterManagement.Client.IoddManagement.Models;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeIdentifier;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal sealed class DeviceFamilyNode : IIoddTreeNode
{
    public INodeIdentifier Id { get; set; } = GuidNodeIdentifier.New();
    public required VendorNode VendorNode { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }

    public List<DeviceView> Devices { get; set; } = [];
}
