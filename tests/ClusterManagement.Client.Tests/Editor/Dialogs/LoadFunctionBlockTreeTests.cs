using Bunit;
using ClusterManagement.Client.Editor.Components;
using ClusterManagement.Client.Editor.Models;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;

namespace ClusterManagement.Client.Tests.Editor.Dialogs;

public class LoadFunctionBlockTreeTests
{
    [Fact]
    public void ComponentGetsRendered()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddDialog();
        ctx.Services.AddCheckBox();

        // Act
        var sut = ctx.Render<LoadFunctionBlockTree>(p =>
        {
            p.Add(a => a.Parent, null);
            p.Add(a => a.FunctionBlockModel, new());
            p.Add(a => a.FunctionBlockModelTemplate, f => f.Name);
        });

        // Assert
        Assert.NotNull(sut);
    }

    public class OnCheckedChanged
    {
        [Fact]
        public async Task Checks_all_sub_function_blocks()
        {
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.Services.AddDialog();
            ctx.Services.AddCheckBox();
            var invoked = false;
            var model = CreateFunctionBlockModel();

            var component = ctx.Render<LoadFunctionBlockTree>(p =>
            {
                p.Add(a => a.Parent, null);
                p.Add(a => a.FunctionBlockModel, model);
                p.Add(a => a.FunctionBlockModelTemplate, f => f.Name);
                p.Add(a => a.CheckChanged, () => invoked = true);
            });

            var headerCheckBoxes = component.FindAll(".header-text > .check-box > input");
            headerCheckBoxes[headerCheckBoxes.Count - 1].Input(new() { Value = true });

            Assert.True(invoked);
            var checkedNodes = component.FindAll(".check-box > input[value=\"True\"]");
            checkedNodes.Count.Should().Be(2);
            checkedNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("StandardFunction");
            checkedNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("Test1");

            var uncheckedNodes = component.FindAll(".check-box > input[value=\"False\"]");
            uncheckedNodes.Count.Should().Be(2);
            uncheckedNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("FunctionBlocks");
            uncheckedNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("Test");

            var indeterminateNodes = component.FindAll(".check-box > input[value=\"null\"]");
            indeterminateNodes.Count.Should().Be(3);
            indeterminateNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("All");
            indeterminateNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("ACX");
            indeterminateNodes[2].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("ViciOne");
        }

        [Fact]
        public async Task Unchecks_all_sub_function_blocks()
        {
            await using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.Services.AddDialog();
            ctx.Services.AddCheckBox();
            var invoked = false;
            var model = CreateFunctionBlockModel(true);

            var component = ctx.Render<LoadFunctionBlockTree>(p =>
            {
                p.Add(a => a.Parent, null);
                p.Add(a => a.FunctionBlockModel, model);
                p.Add(a => a.FunctionBlockModelTemplate, f => f.Name);
                p.Add(a => a.CheckChanged, () => invoked = true);
            });

            var headerCheckBoxes = component.FindAll(".header-text > .check-box > input");
            headerCheckBoxes[headerCheckBoxes.Count - 1].Input(new() { Value = false });

            Assert.True(invoked);
            var uncheckedNodes = component.FindAll(".check-box > input[value=\"False\"]");
            uncheckedNodes.Count.Should().Be(2);
            uncheckedNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("StandardFunction");
            uncheckedNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("Test1");

            var checkedNodes = component.FindAll(".check-box > input[value=\"True\"]");
            checkedNodes.Count.Should().Be(2);
            checkedNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("FunctionBlocks");
            checkedNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("Test");

            var indeterminateNodes = component.FindAll(".check-box > input[value=\"null\"]");
            indeterminateNodes.Count.Should().Be(3);
            indeterminateNodes[0].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("All");
            indeterminateNodes[1].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("ACX");
            indeterminateNodes[2].ParentElement?.NextElementSibling?.InnerHtml.Should().Contain("ViciOne");
        }
    }

    public class OnExpandButtonClick
    {
        [Fact]
        public void Does_not_show_collapsed_section()
        {
            using var ctx = new BunitContext();
            ctx.SetupSuiteServices();
            ctx.Services.AddDialog();
            ctx.Services.AddCheckBox();
            var model = CreateFunctionBlockModel();

            var component = ctx.Render<LoadFunctionBlockTree>(p =>
            {
                p.Add(a => a.Parent, null);
                p.Add(a => a.FunctionBlockModelTemplate, f => f.Name);
                p.Add(a => a.FunctionBlockModel, model);
            });

            component.Find(".dropdown-icon.expanded").PointerDown();

            component
                .Find(".sub-layer-container")
                .InnerHtml
                .Should().Contain("hidden")
                .And.Contain("expanded");
        }
    }

    private static FunctionBlockModel CreateFunctionBlockModel(bool checkValue = false)
    {
        var model = new FunctionBlockModel()
        {
            Name = "All",
        };

        model.FunctionBlocks.Add(new() { Name = "ACX", Parent = model, });
        model.FunctionBlocks[0].FunctionBlocks.Add(new()
        {
            Name = "ViciOne",
            Parent = model.FunctionBlocks[0],
        });
        model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks.Add(new()
        {
            Name = "FunctionBlocks",
            Parent = model.FunctionBlocks[0].FunctionBlocks[0],
        });
        model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks.Add(new()
        {
            Name = "StandardFunction",
            Parent = model.FunctionBlocks[0].FunctionBlocks[0],
        });
        model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks.Add(new()
        {
            Name = "Test",
            Parent = model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[0],
        });
        model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[1].FunctionBlocks.Add(new()
        {
            Name = "Test1",
            Parent = model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[1],
        });

        if (checkValue)
        {
            model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[0].Checked = checkValue;

            model.FunctionBlocks[0].FunctionBlocks[0].FunctionBlocks[1].FunctionBlocks[0].Checked = checkValue;
        }

        return model;
    }
}
