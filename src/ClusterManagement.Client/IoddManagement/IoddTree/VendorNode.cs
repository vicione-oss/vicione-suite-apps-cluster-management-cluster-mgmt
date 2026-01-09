using ViciOne.Ui.TreeEditor.Builder.Interface.NodeIdentifier;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal sealed class VendorNode : IIoddTreeNode
{
    public INodeIdentifier Id { get; set; } = GuidNodeIdentifier.New();
    public required int VendorId { get; init; }
    public required RepositoryNode RepositoryNode { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }

    public OrderedDictionary<string, DeviceFamilyNode> DeviceFamilyNodes { get; } = [];
}
