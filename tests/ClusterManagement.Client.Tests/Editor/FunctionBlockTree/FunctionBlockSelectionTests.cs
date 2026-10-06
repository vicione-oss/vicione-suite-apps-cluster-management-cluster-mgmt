using AngleSharp.Dom;
using Bunit;
using ClusterManagement.Client.Editor.Components;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Cluster.Model;
using ViciOne.Ui.MonochromeIcons.Assets.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace ClusterManagement.Client.Tests.Editor.FunctionBlockTree;

public sealed class FunctionBlockSelectionTests
{
    private static readonly ClusterDependency s_functionBlocksTest = new() { Name = "ACX.ViciOne.FunctionBlocks.Test", Version = "1.0.0" };
    private static readonly ClusterDependency s_standardFunctionTest = new() { Name = "ACX.ViciOne.StandardFunction.Test", Version = "1.0.0" };

    private static IElement FindNode(IRenderedComponent<FunctionBlockSelection> component, string text)
        => component.FindAll(".tree-node").Single(n => n.QuerySelector(".node-text")?.TextContent == text);

    private static IElement FindLeaf(IRenderedComponent<FunctionBlockSelection> component, string text)
        => component.FindAll(".tree-node").Where(n => n.QuerySelector(".node-text")?.TextContent == text).ElementAt(0);

    [Fact]
    public async Task Renders_the_namespaces_of_the_dependencies()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest]));

        component.WaitForAssertion(() =>
        {
            FindNode(component, "ACX").Should().NotBeNull();
            FindNode(component, "FunctionBlocks").Should().NotBeNull();
            FindNode(component, "StandardFunction").Should().NotBeNull();
            component.FindAll(".tree-node").Where(n => n.QuerySelector(".node-text")?.TextContent == "Test").Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task Renders_the_tree_while_the_icons_are_still_loading()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();
        var iconMarkup = new TaskCompletionSource<string?>();
        var iconProvider = Substitute.For<IMonochromeIconSvgMarkupProvider>();
        iconProvider.GetSvgMarkupAsync(Arg.Any<MonochromeIconName>(), Arg.Any<MonochromeIconSize>(), Arg.Any<CancellationToken>()).Returns(iconMarkup.Task);
        ctx.Services.AddSingleton(iconProvider);

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest]));

        component.WaitForAssertion(() => FindNode(component, "ACX").QuerySelector(".icon-container .icon").Should().BeNull());

        await component.InvokeAsync(() => iconMarkup.SetResult("<svg class=\"loaded-icon\"></svg>"));

        component.WaitForAssertion(() => FindNode(component, "ACX").QuerySelector(".loaded-icon").Should().NotBeNull());
    }

    [Fact]
    public async Task Shows_a_message_without_dependencies()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();

        var component = ctx.Render<FunctionBlockSelection>(p => p.Add(c => c.Dependencies, []));

        component.Markup.Should().Contain(Client.Editor.Localization.LoadFunctionBlockDialog.NoFunctionBlockAvailable);
    }

    [Fact]
    public async Task Reports_the_checked_dependency()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();
        IReadOnlyList<ClusterDependency>? reported = null;

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest])
            .Add(c => c.CheckedDependenciesChanged, d => reported = d));

        component.WaitForAssertion(() => FindNode(component, "FunctionBlocks"));
        FindNode(component, "FunctionBlocks").QuerySelector("input")!.Input();

        component.WaitForAssertion(() => reported.Should().Equal(s_functionBlocksTest));
        component.WaitForAssertion(() => FindNode(component, "ACX").QuerySelector(".check-box")!.ClassList.Should().Contain("indeterminate"));
    }

    [Fact]
    public async Task Checks_all_function_blocks()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest]));

        component.Find(".all input").Input();

        component.WaitForAssertion(() => component.Instance.CheckedDependencies.Should().BeEquivalentTo([s_functionBlocksTest, s_standardFunctionTest]));
        component.WaitForAssertion(() => FindLeaf(component, "Test").QuerySelector("input")!.HasAttribute("checked").Should().BeTrue());
    }

    [Fact]
    public async Task Filter_does_not_highlight_matching_text()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest])
            .Add(c => c.Filter, "standard"));

        component.WaitForAssertion(() => component.Markup.Should().Contain(">StandardFunction<").And.NotContain("<b>"));
    }

    [Fact]
    public async Task Checking_all_while_filtered_only_checks_matching_function_blocks()
    {
        await using var ctx = new BunitContext();
        ctx.SetupFunctionBlockSelection();

        var component = ctx.Render<FunctionBlockSelection>(p => p
            .Add(c => c.Dependencies, [s_functionBlocksTest, s_standardFunctionTest])
            .Add(c => c.Filter, "standard"));

        component.Find(".all input").Input();

        component.WaitForAssertion(() => component.Instance.CheckedDependencies.Should().Equal(s_standardFunctionTest));
        // only the matching function blocks are visible and all of them are checked
        component.WaitForAssertion(() => component.Find(".all .check-box").ClassList.Should().NotContain("indeterminate"));
        component.WaitForAssertion(() => component.Find(".all input").HasAttribute("checked").Should().BeTrue());
    }
}
