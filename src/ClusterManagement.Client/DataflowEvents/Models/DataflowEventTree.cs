namespace ClusterManagement.Client.DataflowEvents.Models;

/// <summary>
/// A heading of the events table: a dataflow or a container with the events of the function blocks directly in it and
/// the containers below it, or a flat group without children.
/// </summary>
/// <param name="Group">Every event in and below the node: its own <paramref name="Events"/> first, then those of its <paramref name="Children"/>.</param>
/// <param name="Path">The names from the top of the tree down to the node.</param>
/// <param name="Events">The events of the function blocks directly in the node.</param>
/// <param name="Children">The containers directly below the node.</param>
internal sealed record DataflowEventNode(DataflowEventGroup Group, IReadOnlyList<string> Path, IReadOnlyList<DataflowEventModel> Events,
    IReadOnlyList<DataflowEventNode> Children)
{
    public string Name => Path[^1];
}

internal static class DataflowEventTree
{
    /// <summary>
    /// Builds a node per dataflow that holds events passing <paramref name="direction"/> and <paramref name="filter"/>,
    /// with its containers below it, in the order of <see cref="DataflowEventGroups.Build"/>.
    /// </summary>
    /// <remarks>
    /// A node is known by its path of names, because the events know their containers by name only. Two containers of the
    /// same name next to each other therefore share one node.
    /// </remarks>
    public static IReadOnlyList<DataflowEventNode> Build(IEnumerable<DataflowEventModel> events, DataflowEventDirection direction, string? filter)
    {
        var root = new NodeBuilder([]);
        foreach (var dataflowEvent in DataflowEventGroups.Build(events, DataflowEventGrouping.None, direction, filter).SelectMany(g => g.Events))
        {
            var node = root.Child(dataflowEvent.DataflowName);
            foreach (var container in dataflowEvent.ContainerPath)
                node = node.Child(container);

            node.Events.Add(dataflowEvent);
        }

        return [.. root.Children.Select(child => child.Build())];
    }

    private sealed class NodeBuilder(IReadOnlyList<string> path)
    {
        private readonly Dictionary<string, NodeBuilder> _childrenByName = new(StringComparer.Ordinal);

        public List<NodeBuilder> Children { get; } = [];

        public List<DataflowEventModel> Events { get; } = [];

        public NodeBuilder Child(string name)
        {
            if (!_childrenByName.TryGetValue(name, out var child))
            {
                child = new NodeBuilder([.. path, name]);
                _childrenByName.Add(name, child);
                Children.Add(child);
            }

            return child;
        }

        public DataflowEventNode Build()
        {
            DataflowEventNode[] children = [.. Children.Select(child => child.Build())];
            var group = new DataflowEventGroup(KeyOf(path), [.. Events, .. children.SelectMany(child => child.Group.Events)]);
            return new DataflowEventNode(group, path, Events, children);
        }

        // Each name carries its length, so no two paths give the same key, whatever the names hold.
        private static string KeyOf(IReadOnlyList<string> path)
            => string.Concat(path.Select(name => $"{name.Length}:{name}"));
    }
}
