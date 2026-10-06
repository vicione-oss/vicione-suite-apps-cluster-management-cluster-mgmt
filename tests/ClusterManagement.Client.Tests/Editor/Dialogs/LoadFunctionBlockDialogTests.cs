using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ClusterManagement.Client.Editor.Dialogs;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Tests.Editor.FunctionBlockTree;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;
using ViciOne.Ui.ClusterEditor.Services;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.Editor.Dialogs;

public sealed class LoadFunctionBlockDialogTests
{
    private readonly IDesignProvider _designProvider = Substitute.For<IDesignProvider>();
    private readonly IClusterEditorManagement _clusterEditorManagement = Substitute.For<IClusterEditorManagement>();

    private readonly ClusterDependency _functionBlocksTest = new() { Name = "ACX.ViciOne.FunctionBlocks.Test", Version = "1.0.0" };
    private readonly ClusterDependency _standardFunctionTest = new() { Name = "ACX.ViciOne.StandardFunction.Test", Version = "1.0.0" };

    private LoadFunctionBlocksService SetupServices(BunitContext ctx)
    {
        var service = new LoadFunctionBlocksService(
            _designProvider,
            _clusterEditorManagement,
            Substitute.For<ILogger<LoadFunctionBlocksService>>());

        ctx.SetupFunctionBlockSelection();
        ctx.Services.AddDialog();
        ctx.Services.AddToolbar();
        ctx.Services.AddSingleton(service);
        ctx.Services.AddSingleton(Substitute.For<ILogger<LoadFunctionBlockDialog>>());

        _designProvider.GetClusterDependencies().Returns([_functionBlocksTest, _standardFunctionTest]);

        return service;
    }

    private static async Task<IRenderedComponent<Dialog>> RenderOpenDialog(BunitContext ctx, LoadFunctionBlocksService service, params ClusterDependency[]? preSelected)
    {
        await service.LoadPackageReferences(preSelected);

        var component = ctx.Render<LoadFunctionBlockDialog>();
        var dialog = component.FindComponent<Dialog>();
        dialog.WaitForState(dialog.HasComponent<Microsoft.AspNetCore.Components.Sections.SectionContent>);

        return dialog;
    }

    /// <summary>
    /// Renders the popup content (the tree) of the dialog. It is rendered detached and does not follow later state changes of the dialog.
    /// </summary>
    private static IRenderedComponent<ContainerFragment> RenderBody(BunitContext ctx, IRenderedComponent<Dialog> dialog)
    {
        var content = dialog.RenderSectionContent(ctx);
        content.WaitForAssertion(() => content.FindAll(".tree-node").Should().NotBeEmpty());

        return content;
    }

    /// <summary>
    /// Renders the footer with the current state of the dialog.
    /// </summary>
    private static IElement RenderOkButton(BunitContext ctx, IRenderedComponent<Dialog> dialog)
        => ctx.Render(dialog.Instance.Footer!).FindAll("button").First(b => b.TextContent.Contains(CommonVocabulary.Ok, StringComparison.Ordinal));

    private static IElement GetCancelButton(IRenderedComponent<ContainerFragment> content)
        => content.FindAll(".footer button").First(b => b.TextContent.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));

    private static IElement FindNode(IRenderedComponent<ContainerFragment> content, string text)
        => content.FindAll(".tree-node").First(n => n.QuerySelector(".node-text")?.TextContent == text);

    private static void WaitForEnabledOkButton(BunitContext ctx, IRenderedComponent<Dialog> dialog)
        => dialog.WaitForAssertion(() => RenderOkButton(ctx, dialog).HasAttribute("disabled").Should().BeFalse());

    [Fact]
    public async Task Renders_without_open_dialog()
    {
        await using var ctx = new BunitContext();
        SetupServices(ctx);

        var sut = ctx.Render<LoadFunctionBlockDialog>();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task Ok_is_disabled_until_a_function_block_is_checked()
    {
        await using var ctx = new BunitContext();
        var service = SetupServices(ctx);
        var dialog = await RenderOpenDialog(ctx, service);
        var body = RenderBody(ctx, dialog);

        RenderOkButton(ctx, dialog).HasAttribute("disabled").Should().BeTrue();

        await FindNode(body, "FunctionBlocks").QuerySelector("input")!.InputAsync(Xunit.TestContext.Current.CancellationToken);

        WaitForEnabledOkButton(ctx, dialog);
    }

    [Fact]
    public async Task Loads_only_the_checked_function_blocks()
    {
        await using var ctx = new BunitContext();
        var service = SetupServices(ctx);
        var dialog = await RenderOpenDialog(ctx, service);
        var body = RenderBody(ctx, dialog);

        await FindNode(body, "StandardFunction").QuerySelector("input")!.InputAsync(Xunit.TestContext.Current.CancellationToken);
        WaitForEnabledOkButton(ctx, dialog);

        await RenderOkButton(ctx, dialog).ClickAsync();

        _ = _designProvider.Received(1).GetFunctionBlockDesignIds(Arg.Is<IReadOnlyCollection<ClusterDependency>>(d => d.SequenceEqual(new[] { _standardFunctionTest })));
        _clusterEditorManagement.Received(1).LoadFunctionBlockDesigns(Arg.Any<IEnumerable<Guid>>());
        service.SelectedDependencies.Should().Equal(_standardFunctionTest);
        service.DisplayDialog.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_offer_already_loaded_function_blocks()
    {
        await using var ctx = new BunitContext();
        var service = SetupServices(ctx);
        var dialog = await RenderOpenDialog(ctx, service, [_functionBlocksTest]);

        var body = RenderBody(ctx, dialog);

        FindNode(body, "StandardFunction").Should().NotBeNull();
        body.FindAll(".node-text").Select(n => n.TextContent).Should().NotContain("FunctionBlocks");
    }

    [Fact]
    public async Task Cancel_closes_the_dialog_without_loading()
    {
        await using var ctx = new BunitContext();
        var service = SetupServices(ctx);
        var dialog = await RenderOpenDialog(ctx, service);
        var body = RenderBody(ctx, dialog);

        await GetCancelButton(body).ClickAsync();

        service.DisplayDialog.Should().BeFalse();
        _clusterEditorManagement.DidNotReceive().LoadFunctionBlockDesigns(Arg.Any<IEnumerable<Guid>>());
    }
}
