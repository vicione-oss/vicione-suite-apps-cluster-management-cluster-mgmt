using ClusterManagement.Client.Editor.FunctionBlockTree;
using Microsoft.AspNetCore.Components.Web;
using ViciOne.Cluster.Model;
using ViciOne.Ui.MonochromeIcons.Assets.Services;
using ViciOne.Ui.TreeEditor.Builder;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions.Arguments;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace ClusterManagement.Client.Tests.Editor.FunctionBlockTree;

public sealed class FunctionBlockTreeAdapterTests : IDisposable
{
    private static readonly ClusterDependency s_functionBlocksTest = new() { Name = "ACX.ViciOne.FunctionBlocks.Test", Version = "1.0.0" };
    private static readonly ClusterDependency s_standardFunctionTest = new() { Name = "ACX.ViciOne.StandardFunction.Test", Version = "1.0.0" };
    private static readonly ClusterDependency s_standardFunctionAbc = new() { Name = "ACX.ViciOne.StandardFunction.ABC", Version = "1.0.0" };
    private static readonly ClusterDependency s_standardFunctionAbcd = new() { Name = "ACX.ViciOne.StandardFunction.ABCD", Version = "1.0.0" };

    private readonly TreeBuilder _treeBuilder = new();
    private readonly FunctionBlockTreeAdapter _sut = new(Substitute.For<IMonochromeIconSvgMarkupProvider>());

    public FunctionBlockTreeAdapterTests()
    {
        _treeBuilder.SetAdapter(_sut);
        _sut.SetDependencies([s_functionBlocksTest, s_standardFunctionTest, s_standardFunctionAbc, s_standardFunctionAbcd]);
    }

    public void Dispose()
        => _treeBuilder.Dispose();

    private ITreeNode Node(string path)
    {
        var segments = path.Split('.');
        var node = _sut.GetRootNodes().Single(n => _sut.GetDisplayText(n) == segments[0]);

        foreach (var segment in segments.Skip(1))
            node = _sut.GetChildren(node).Single(n => _sut.GetDisplayText(n) == segment);

        return node;
    }

    [Fact]
    public void Builds_tree_from_dotted_names()
    {
        var root = Assert.Single(_sut.GetRootNodes());
        _sut.GetDisplayText(root).Should().Be("ACX");

        _sut.GetChildren(Node("ACX.ViciOne")).Select(_sut.GetDisplayText).Should().Equal("FunctionBlocks", "StandardFunction");
        _sut.GetChildren(Node("ACX.ViciOne.StandardFunction")).Select(_sut.GetDisplayText).Should().Equal("ABC", "ABCD", "Test");

        _sut.GetParent(Node("ACX.ViciOne")).Should().BeSameAs(root);
        _sut.GetParent(root).Should().BeNull();
        _sut.HasFunctionBlocks.Should().BeTrue();
    }

    [Fact]
    public void Shows_nothing_without_dependencies()
    {
        _sut.SetDependencies([]);

        _sut.GetRootNodes().Should().BeEmpty();
        _sut.HasFunctionBlocks.Should().BeFalse();
        _sut.AllCheckState.Should().BeFalse();
    }

    [Fact]
    public void Keeps_equally_named_function_blocks_in_different_namespaces_apart()
    {
        _sut.SetChecked(Node("ACX.ViciOne.FunctionBlocks.Test"), true);

        _sut.CheckedDependencies.Should().Equal(s_functionBlocksTest);
        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction.Test")).Should().BeFalse();
    }

    [Fact]
    public void Checking_a_namespace_checks_all_descendants()
    {
        _sut.SetChecked(Node("ACX.ViciOne.StandardFunction"), true);

        _sut.CheckedDependencies.Should().BeEquivalentTo([s_standardFunctionTest, s_standardFunctionAbc, s_standardFunctionAbcd]);
        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction.ABC")).Should().BeTrue();
    }

    [Fact]
    public void Namespace_is_indeterminate_when_only_some_descendants_are_checked()
    {
        _sut.SetChecked(Node("ACX.ViciOne.StandardFunction.ABC"), true);

        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction")).Should().BeNull();
        _sut.GetCheckState(Node("ACX")).Should().BeNull();
        _sut.GetCheckState(Node("ACX.ViciOne.FunctionBlocks")).Should().BeFalse();
        _sut.AllCheckState.Should().BeNull();
    }

    [Fact]
    public void Checks_and_unchecks_all()
    {
        _sut.SetAllChecked(true);
        _sut.AllCheckState.Should().BeTrue();
        _sut.CheckedDependencies.Should().HaveCount(4);

        _sut.SetAllChecked(false);
        _sut.AllCheckState.Should().BeFalse();
        _sut.CheckedDependencies.Should().BeEmpty();
    }

    [Fact]
    public void Checking_a_namespace_while_filtered_only_checks_matching_function_blocks()
    {
        _sut.SetFilter("abc");

        _sut.SetChecked(Node("ACX.ViciOne.StandardFunction"), true);

        _sut.CheckedDependencies.Should().BeEquivalentTo([s_standardFunctionAbc, s_standardFunctionAbcd]);
        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction")).Should().BeTrue();
    }

    [Fact]
    public void Check_state_only_covers_matching_function_blocks_while_filtered()
    {
        _sut.SetFilter("abcd");

        _sut.SetChecked(Node("ACX.ViciOne.StandardFunction.ABCD"), true);

        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction")).Should().BeTrue();
        _sut.GetCheckState(Node("ACX")).Should().BeTrue();
        _sut.AllCheckState.Should().BeTrue();

        _sut.SetFilter(null);

        _sut.GetCheckState(Node("ACX.ViciOne.StandardFunction")).Should().BeNull();
        _sut.AllCheckState.Should().BeNull();
    }

    [Fact]
    public void Unchecks_all_matching_function_blocks_while_filtered()
    {
        _sut.SetChecked(Node("ACX.ViciOne.FunctionBlocks.Test"), true);
        _sut.SetFilter("abc");

        _sut.SetAllChecked(true);
        _sut.AllCheckState.Should().BeTrue();

        _sut.SetAllChecked(false);
        _sut.AllCheckState.Should().BeFalse();

        // the hidden check is kept
        _sut.CheckedDependencies.Should().Equal(s_functionBlocksTest);
    }

    [Fact]
    public void Keeps_checks_when_the_filter_changes()
    {
        _sut.SetChecked(Node("ACX.ViciOne.FunctionBlocks.Test"), true);

        _sut.SetFilter("abc");
        _sut.SetFilter(null);

        _sut.CheckedDependencies.Should().Equal(s_functionBlocksTest);
    }

    [Fact]
    public void Keeps_checks_of_dependencies_that_are_still_present()
    {
        _sut.SetChecked(Node("ACX.ViciOne.FunctionBlocks.Test"), true);
        _sut.SetChecked(Node("ACX.ViciOne.StandardFunction.ABC"), true);

        _sut.SetDependencies([s_functionBlocksTest, s_standardFunctionTest]);

        _sut.CheckedDependencies.Should().Equal(s_functionBlocksTest);
    }

    [Fact]
    public void Namespaces_with_matching_descendants_pass_the_filter()
    {
        _sut.SetFilter("abcd");

        Predicate<ITreeNode> filter = _treeBuilder.Filter.Match;

        _treeBuilder.Filter.Match(Node("ACX.ViciOne.StandardFunction.ABCD")).Should().BeTrue();
        _treeBuilder.Filter.Match(Node("ACX.ViciOne.StandardFunction.ABC")).Should().BeFalse();
        _sut.HasFilterMatchingDescendants(Node("ACX.ViciOne.StandardFunction"), filter).Should().BeTrue();
        _sut.HasFilterMatchingDescendants(Node("ACX.ViciOne.FunctionBlocks"), filter).Should().BeFalse();
    }

    [Fact]
    public void Space_toggles_the_check()
    {
        var node = Node("ACX.ViciOne.FunctionBlocks");
        var action = Assert.Single(_sut.GetActions(node).OfType<NodeKeyboardAction>());
        action.TriggerKey.MatchesEventArgs(new KeyboardEventArgs { Code = "Space" }).Should().BeTrue();

        action.InvokeAction(new ActionArguments(_treeBuilder, node));
        _sut.GetCheckState(node).Should().BeTrue();

        action.InvokeAction(new ActionArguments(_treeBuilder, node));
        _sut.GetCheckState(node).Should().BeFalse();
    }

    [Fact]
    public void Raises_checked_changed()
    {
        var raised = 0;
        _sut.CheckedChanged += () => raised++;

        _sut.SetChecked(Node("ACX"), true);

        raised.Should().Be(1);
    }
}
