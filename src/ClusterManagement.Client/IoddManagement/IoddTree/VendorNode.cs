using ViciOne.Ui.TreeEditor.Builder.Interface;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal class VendorNode : ITreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required int VendorId { get; init; }
    public required RepositoryNode RepositoryNode { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }
    public bool Selected { get; set; }

    public OrderedDictionary<string, DeviceFamilyNode> DeviceFamilyNodes { get; } = [];
}
