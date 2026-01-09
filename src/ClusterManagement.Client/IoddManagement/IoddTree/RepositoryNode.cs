using ViciOne.Ui.TreeEditor.Builder.Interface;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal class RepositoryNode : ITreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required int RepositoryId { get; init; }

    public required string DisplayText { get; set; }
    public bool Expanded { get; set; }
    public bool Selected { get; set; }

    public OrderedDictionary<int, VendorNode> VendorNodes { get; } = [];
}
