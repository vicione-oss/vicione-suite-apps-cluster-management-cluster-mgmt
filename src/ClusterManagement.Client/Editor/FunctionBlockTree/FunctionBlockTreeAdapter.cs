using ViciOne.Cluster.Model;
using ViciOne.Ui.MonochromeIcons.Assets.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.TreeEditor.Builder.Interface;
using ViciOne.Ui.TreeEditor.Builder.Interface.Enums;
using ViciOne.Ui.TreeEditor.Builder.Interface.Icons;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions.Other;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace ClusterManagement.Client.Editor.FunctionBlockTree;

/// <summary>
/// Shows dependencies as a tree of their dotted name segments and keeps track of which ones are checked.
/// </summary>
internal sealed class FunctionBlockTreeAdapter(IMonochromeIconSvgMarkupProvider iconMarkupProvider) : TreeAdapter
{
    private static readonly MonochromeIconSize s_iconSize = MonochromeIconSize.Medium;

    private static readonly MonochromeIconName s_namespaceIcon = MonochromeIconName.Folder;
    private static readonly MonochromeIconName s_functionBlockIcon = MonochromeIconName.Library;

    private readonly Dictionary<MonochromeIconName, IIcon> _icons = [];

    // keyed by the full dependency name, so equally named segments in different namespaces don't collide
    private readonly HashSet<string> _checkedNames = new(StringComparer.Ordinal);

    // invisible parent of all root nodes
    private FunctionBlockTreeNode _root = new() { Name = string.Empty };
    private Task? _initializeTask;

    public event Action? CheckedChanged;

    public bool HasFunctionBlocks => _root.Children.Count > 0;

    public bool? AllCheckState => GetCheckState(_root);

    public IReadOnlyList<ClusterDependency> CheckedDependencies
        => [.. DependenciesOf(_root).Where(d => _checkedNames.Contains(d.Name))];

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _initializeTask ??= InitializeCore(cancellationToken);

    private async Task InitializeCore(CancellationToken cancellationToken)
    {
        await LoadIcon(s_namespaceIcon, cancellationToken);
        await LoadIcon(s_functionBlockIcon, cancellationToken);

        // nodes may already have been created without icons; Builder is null once the tree builder got disposed
        Builder?.Notifications.NotifyNodeChanged(_root.SelfAndDescendants().Skip(1), ChangedNodeDetail.Icons);
    }

    private async Task LoadIcon(MonochromeIconName iconName, CancellationToken cancellationToken)
    {
        var svgMarkup = await iconMarkupProvider.GetSvgMarkupAsync(iconName, s_iconSize, cancellationToken);

        _icons[iconName] = string.IsNullOrEmpty(svgMarkup)
            ? new SvgIcon(string.Empty) { CssClasses = iconName.GetCssClasses(s_iconSize) }
            : new SvgIcon(svgMarkup);
    }

    public override void Setup()
    {
        Builder.DragAndDrop.EnableInbound = false;
        Builder.DragAndDrop.EnableInternal = false;
        Builder.DragAndDrop.EnableOutbound = false;

        Builder.Template.Mapping = (_, templateType) => templateType == TemplateType.Node ? typeof(FunctionBlockCheckNode) : null;
    }

    /// <summary>
    /// Rebuilds the tree from the given dependencies. Checks of dependencies that are still present are kept.
    /// </summary>
    public void SetDependencies(IEnumerable<ClusterDependency> dependencies)
    {
        var root = new FunctionBlockTreeNode { Name = string.Empty };

        foreach (var dependency in dependencies)
        {
            var node = root;
            foreach (var segment in dependency.Name.Split('.'))
                node = node.GetOrAddChild(segment);

            node.Dependency = dependency;
        }

        _root = root;
        _checkedNames.IntersectWith(DependenciesOf(_root).Select(d => d.Name));

        Builder.Notifications.NotifyRootNodesChanged();
        CheckedChanged?.Invoke();
    }

    public void SetFilter(string? filterText)
    {
        Predicate<ITreeNode>[] filter = string.IsNullOrEmpty(filterText)
            ? []
            : [n => n is FunctionBlockTreeNode { Dependency: { } d } && d.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)];

        Builder.Filter.SetFilter(filter);

        // the check states only cover matching function blocks, so they change even for nodes whose visibility doesn't
        Builder.Helper.RequestRefreshAll();
    }

    public void ExpandAll()
        => Builder.Expansion.ChangeExpansionForLayers(true);

    public void CollapseAll()
        => Builder.Expansion.ChangeExpansionForLayers(false);

    /// <summary>
    /// Derives the state from the function blocks below the node. While a filter is active, only matching ones count.
    /// </summary>
    public bool? GetCheckState(ITreeNode treeNode)
    {
        if (treeNode is not FunctionBlockTreeNode node)
            return false;

        var anyChecked = false;
        var anyUnchecked = false;

        foreach (var dependency in SelectableDependenciesOf(node))
        {
            if (_checkedNames.Contains(dependency.Name))
                anyChecked = true;
            else
                anyUnchecked = true;

            if (anyChecked && anyUnchecked)
                return null;
        }

        return anyChecked;
    }

    /// <summary>
    /// Checks or unchecks the node and all its descendants. While a filter is active, only matching ones are changed.
    /// </summary>
    public void SetChecked(ITreeNode treeNode, bool check)
    {
        if (treeNode is FunctionBlockTreeNode node)
            SetChecked(node, check);
    }

    public void SetAllChecked(bool check)
        => SetChecked(_root, check);

    private void SetChecked(FunctionBlockTreeNode node, bool check)
    {
        foreach (var dependency in SelectableDependenciesOf(node))
        {
            if (check)
                _checkedNames.Add(dependency.Name);
            else
                _checkedNames.Remove(dependency.Name);
        }

        // ancestors and descendants display a derived state, the tree is small enough to refresh all visible nodes
        Builder.Helper.RequestRefreshAll();
        CheckedChanged?.Invoke();
    }

    private void ToggleChecked(ITreeNode treeNode)
        => SetChecked(treeNode, GetCheckState(treeNode) is not true);

    private static IEnumerable<ClusterDependency> DependenciesOf(FunctionBlockTreeNode node)
        => node.SelfAndDescendants().Select(n => n.Dependency).OfType<ClusterDependency>();

    // the dependencies the user can see and therefore (un)check, i.e. the ones matching an active filter
    private IEnumerable<ClusterDependency> SelectableDependenciesOf(FunctionBlockTreeNode node)
    {
        var filterActive = Builder.Filter.IsFilterActive;

        return node.SelfAndDescendants()
            .Where(n => !filterActive || Builder.Filter.Match(n))
            .Select(n => n.Dependency)
            .OfType<ClusterDependency>();
    }

    public override IEnumerable<INodeAction> GetActions(ITreeNode treeNode)
        => [new NodeKeyboardAction { TriggerKey = new KeyboardKey { Code = "Space" }, Action = (_, args) => ToggleChecked(args.Node) }];

    public override IEnumerable<ITreeNode> GetChildren(ITreeNode treeNode)
        => treeNode is FunctionBlockTreeNode node ? node.Children : [];

    public override string GetDisplayText(ITreeNode treeNode)
        => treeNode is FunctionBlockTreeNode node ? node.Name : $"{treeNode.Id}";

    public override IEnumerable<IIcon> GetIcons(ITreeNode treeNode)
    {
        var iconName = treeNode is FunctionBlockTreeNode { Children.Count: 0 } ? s_functionBlockIcon : s_namespaceIcon;

        return _icons.TryGetValue(iconName, out var icon) ? [icon] : [];
    }

    public override ITreeNode? GetParent(ITreeNode treeNode)
        => treeNode is FunctionBlockTreeNode { Parent: { } parent } && parent != _root ? parent : null;

    public override IEnumerable<ITreeNode> GetRootNodes()
        => _root.Children;

    public override bool HasChildren(ITreeNode treeNode)
        => treeNode is FunctionBlockTreeNode { Children.Count: > 0 };

    public override bool HasFilterMatchingDescendants(ITreeNode treeNode, Predicate<ITreeNode> filter)
        => treeNode is FunctionBlockTreeNode node && node.SelfAndDescendants().Skip(1).Any(d => filter(d));

    public override bool IsExpanded(ITreeNode treeNode)
        => true;
}
