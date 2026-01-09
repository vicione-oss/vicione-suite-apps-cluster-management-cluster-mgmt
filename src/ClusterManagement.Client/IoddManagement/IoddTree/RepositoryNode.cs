using ViciOne.Ui.TreeEditor.Builder.Interface.NodeIdentifier;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal sealed class RepositoryNode : IIoddTreeNode
{
    public INodeIdentifier Id { get; set; } = GuidNodeIdentifier.New();
    public required int RepositoryId { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }

    public OrderedDictionary<int, VendorNode> VendorNodes { get; } = [];
}
