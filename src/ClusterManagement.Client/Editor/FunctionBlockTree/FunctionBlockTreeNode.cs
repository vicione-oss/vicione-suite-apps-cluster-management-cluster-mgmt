using ViciOne.Cluster.Model;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeIdentifier;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace ClusterManagement.Client.Editor.FunctionBlockTree;

/// <summary>
/// A segment of a dotted dependency name, e.g. <c>ViciOne</c> in <c>ACX.ViciOne.FunctionBlocks</c>.
/// Carries a <see cref="Dependency"/> if a dependency name ends at this segment.
/// </summary>
internal sealed class FunctionBlockTreeNode : ITreeNode
{
    public INodeIdentifier Id { get; } = GuidNodeIdentifier.New();

    public required string Name { get; init; }
    public FunctionBlockTreeNode? Parent { get; init; }
    public ClusterDependency? Dependency { get; set; }

    public List<FunctionBlockTreeNode> Children { get; } = [];

    public IEnumerable<FunctionBlockTreeNode> SelfAndDescendants()
    {
        yield return this;

        foreach (var descendant in Children.SelectMany(c => c.SelfAndDescendants()))
            yield return descendant;
    }

    public FunctionBlockTreeNode GetOrAddChild(string name)
    {
        var index = Children.FindIndex(c => string.CompareOrdinal(c.Name, name) >= 0);
        if (index >= 0 && Children[index].Name == name)
            return Children[index];

        var child = new FunctionBlockTreeNode { Name = name, Parent = this };
        Children.Insert(index < 0 ? Children.Count : index, child);
        return child;
    }
}
