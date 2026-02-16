using AngleSharp.Dom;
using Bunit;
using ClusterManagement.Client.Editor.Dialogs;
using ClusterManagement.Client.Editor.Enums;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Public.Designs;
using DevExpress.Blazor.Internal.Editors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;
using ViciOne.Ui.ClusterEditor.Services;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.Editor.Dialogs;

public class LoadFunctionBlockDialogTests
{
    private const string SkipMessage = "The dialog will be refactored and till then the tests will be disabled because they fail too often";

    private readonly IDesignProvider _designProvider = Substitute.For<IDesignProvider>();
    private readonly IDataManagementService _dataManagementService = Substitute.For<IDataManagementService>();

    private readonly ClusterDependency _dependencyFunctionBlockTest = new()
    {
        Name = "ACX.ViciOne.FunctionBlocks.Test",
        Version = new Version(1, 0, 0)
    };

    private readonly ClusterDependency _dependencyStandardFunctionTest = new()
    {
        Name = "ACX.ViciOne.StandardFunction.Test",
        Version = new Version(1, 0, 0)
    };

    private readonly ClusterDependency _dependencyStandardFunctionABC = new()
    {
        Name = "ACX.ViciOne.StandardFunction.ABC",
        Version = new Version(1, 0, 0)
    };

    private readonly ClusterDependency _dependencyStandardFunctionABCD = new()
    {
        Name = "ACX.ViciOne.StandardFunction.ABCD",
        Version = new Version(1, 0, 0)
    };

    private LoadFunctionBlocksService SetupServices(BunitContext ctx)
    {
        var service = new LoadFunctionBlocksService(
            _designProvider,
            _dataManagementService,
            Substitute.For<ILogger<LoadFunctionBlocksService>>());

        ctx.SetupSuiteServices();
        ctx.SetupDevExpressBlazor();
        ctx.Services.AddSingleton(service);
        ctx.Services.AddSingleton(Substitute.For<ILogger<LoadFunctionBlockDialog>>);

        return service;
    }

    private static IElement GetFunctionBlockByName(IRenderedComponent<LoadFunctionBlockDialog> component, string functionBlockName)
        => component.FindAll("dxbl-check").First(fb => fb.InnerHtml.Contains(functionBlockName, StringComparison.OrdinalIgnoreCase));

    private static IElement FindToolbarButton(IRenderedComponent<LoadFunctionBlockDialog> component, SvgIcon icon)
        => component.FindAll(".btn-toolbar").First(k => k.InnerHtml.Contains(icon.GetPath(), StringComparison.OrdinalIgnoreCase));

    private static IElement GetCancelButton(IRenderedComponent<LoadFunctionBlockDialog> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));

    private static IElement GetOkButton(IRenderedComponent<LoadFunctionBlockDialog> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Ok, StringComparison.Ordinal));

    private static void SetSearchFilter(IRenderedComponent<LoadFunctionBlockDialog> component, string filter)
        => component.Find(".dxbl-text-edit").TriggerEvent("ondxbl-input-editor.textinput", new EditorTextInputEventArgs { Text = filter, Version = 0 });

    public class RenderComponent : LoadFunctionBlockDialogTests
    {
        [Fact]
        public void ComponentGetsRendered()
        {
            // Arrange
            using var ctx = new BunitContext();
            SetupServices(ctx);

            // Act
            var sut = ctx.Render<LoadFunctionBlockDialog>();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class SetTreeList : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Generates_tree()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
            ]);

            await service.LoadPackageReferences();

            // Act
            var component = ctx.Render<LoadFunctionBlockDialog>();

            // Assert
            component.WaitForAssertion(() =>
            {
                component.Find(".all").Should().NotBeNull();
                var container = component.Find(".sub-layer-container");

                container.ParentElement?.InnerHtml.Should().Contain("ACX");
                var children = Assert.Single(container.Children);
                children.InnerHtml.Should().Contain("ViciOne");
                children = children.Children.First(e => e.ClassList.Contains("sub-layer-container"));
                children.Children.First().InnerHtml.Should().Contain("FunctionBlocks");
                children.Children.Last().InnerHtml.Should().Contain("StandardFunction");
                children.Children.First().Children.Last().InnerHtml.Should().Contain("Test");
                children.Children.Last().Children.Last().InnerHtml.Should().Contain("Test");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Shows_message_when_dependencies_are_empty()
        {
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns([]);

            await service.LoadPackageReferences();

            // Act
            var component = ctx.Render<LoadFunctionBlockDialog>();

            // Assert
            component.WaitForAssertion(() =>
            {
                component.Markup.Should().Contain(Client.Editor.Localization.LoadFunctionBlockDialog.NoFunctionBlockAvailable);
            });
        }
    }

    public class OnCancelButtonClick : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Closes_popup()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns([]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetCancelButton(component).Click();

                // Assert
                component.Markup.Should().NotContain(Client.Editor.Localization.LoadFunctionBlockDialog.Header);
            }, TimeSpan.FromSeconds(2));    // double time
        }
    }

    public class OnCollapseAllClick : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task All_nodes_get_collapsed()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            // DX Toolbar is rendered async isn't available after rendering
            component.WaitForAssertion(() =>
            {
                // Act
                var button = FindToolbarButton(component, SvgIcon.CollapseAllRows);
                button.Click();

                // Assert
                component.Markup.Should().NotContain("dropdown-icon expanded");
            });
        }
    }

    public class OnCheckedChanged : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Checks_all_function_blocks()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                component.Find("dxbl-check").Change(new() { Value = true });

                // Assert
                component.Markup.Should().NotContain("unchecked").And.NotContain("indeterminate");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Unchecks_all_function_blocks()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                var checkbox = component.Find("dxbl-check");
                checkbox.Change(new() { Value = true });
                checkbox.Change(new() { Value = false });

                // Assert
                component.Markup.Should().NotContain("-checked").And.NotContain("indeterminate");
            });
        }
    }

    public class OnExpandAllClick : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task All_nodes_get_expanded()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                var buttonCollapse = FindToolbarButton(component, SvgIcon.CollapseAllRows);
                buttonCollapse.Click();

                var buttonExpand = FindToolbarButton(component, SvgIcon.ExpandAllRows);
                buttonExpand.Click();

                // Assert
                component.Markup.Should().Contain("dropdown-icon expanded");
            });
        }
    }

    public class OnOkButtonClick : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Loads_only_checked_function_blocks()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionABC,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "abc").Change(new() { Value = true });
                GetOkButton(component).Click();

                // Assert
                _dataManagementService.Received(1).LoadFunctionBlockDesigns(Arg.Any<IEnumerable<Guid>>());
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Loads_checked_functionblock_even_if_not_shown_in_current_filter()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionABC,
            ]);

            await service.LoadPackageReferences();
            service.SelectedDependencies = [];

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "test").Change(new() { Value = true });
                SetSearchFilter(component, "abc");
                GetOkButton(component).Click();

                // Assert
                _dataManagementService.Received(1).LoadFunctionBlockDesigns(Arg.Any<IEnumerable<Guid>>());
            });
        }
    }

    public class ShowFilteredFunctionBlocks : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Checks_all_functionblocks_when_filtering()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
                _dependencyStandardFunctionABC,
                _dependencyStandardFunctionABCD,
            ]);

            await service.LoadPackageReferences();
            service.SelectedDependencies = [];

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "Block").Change(new() { Value = true });
                SetSearchFilter(component, "nB"); // filter only checked fb

                // Assert
                foreach (var cb in component.FindAll(".dxbl-checkbox"))
                    cb.OuterHtml.Should().NotContain("unchecked").And.NotContain("indeterminate");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Checks_indeterminate_when_filtering()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyStandardFunctionABC,
                _dependencyStandardFunctionABCD,
            ]);

            await service.LoadPackageReferences();
            service.SelectedDependencies = [];

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "abc").Change(new() { Value = true });
                SetSearchFilter(component, "abc");

                // Assert
                var parent = GetFunctionBlockByName(component, "Standard");
                parent.OuterHtml.Should().Contain("indeterminate");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Keeps_check_value_when_filtering()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyStandardFunctionABC,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "abc").Change(new() { Value = true });
                SetSearchFilter(component, "abc");

                // Assert
                GetFunctionBlockByName(component, "abc").OuterHtml.Should().Contain("dxbl-checkbox-checked");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Unchecks_all_functionblocks_when_filtering()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
                _dependencyStandardFunctionABC,
                _dependencyStandardFunctionABCD,
            ]);

            await service.LoadPackageReferences();
            service.SelectedDependencies = [];

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "Test").Change(new() { Value = true });
                SetSearchFilter(component, "abc"); // filter only unchecked fb

                // Assert
                foreach (var cb in component.FindAll(".dxbl-checkbox"))
                    cb.OuterHtml.Should().NotContain("dxbl-checkbox-checked").And.NotContain("indeterminate");
            });
        }


        [Fact(Skip = SkipMessage)]
        public async Task Only_shows_filtered_functionblocks()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionABC,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();
            service.SelectedDependencies = [];

            component.WaitForAssertion(() =>
            {
                // Act
                SetSearchFilter(component, "test");

                // Assert
                component.Markup.Should().Contain("Test").And.NotContain("ABC");
            });
        }
    }

    public class ResetTree : LoadFunctionBlockDialogTests
    {
        [Fact(Skip = SkipMessage)]
        public async Task Resets_check_states()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                GetFunctionBlockByName(component, "nB").Change(new() { Value = true });
                GetCancelButton(component).Click();

                service.DisplayDialog = true;

                // Assert
                foreach (var cb in component.FindAll(".dxbl-checkbox"))
                    cb.OuterHtml.Should().NotContain("dxbl-checkbox-checked");
            });
        }

        [Fact(Skip = SkipMessage)]
        public async Task Resets_filter_and_highlighting()
        {
            // Arrange
            using var ctx = new BunitContext();
            var service = SetupServices(ctx);

            _designProvider.GetClusterDependencies().Returns(
            [
                _dependencyFunctionBlockTest,
                _dependencyStandardFunctionTest,
            ]);

            await service.LoadPackageReferences();

            var component = ctx.Render<LoadFunctionBlockDialog>();

            component.WaitForAssertion(() =>
            {
                // Act
                SetSearchFilter(component, "Vici");
                GetCancelButton(component).Click();

                service.DisplayDialog = true;

                // Assert
                component.Markup.Should().NotContain("searchHighlighter");
                component.Find(".dxbl-text-edit").OuterHtml.Should().NotContain("Vici");
            });
        }
    }
}
