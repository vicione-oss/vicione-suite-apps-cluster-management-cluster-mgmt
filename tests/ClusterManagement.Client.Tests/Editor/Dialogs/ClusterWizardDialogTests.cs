using Bunit;
using ClusterManagement.Client.Editor.Dialogs;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Tests.Tools;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Client;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;

namespace ClusterManagement.Client.Tests.Editor.Dialogs;

public class ClusterWizardDialogTests
{
    private static ClusterWizardDialogService SetupServices(BunitContext ctx)
    {
        var dialogService = new ClusterWizardDialogService(ctx.Services, Substitute.For<ILogger<ClusterWizardDialogService>>());

        ctx.SetupSuiteServices();
        ctx.Services
            .AddSingleton(Substitute.For<ILogger<ClusterWizardDialogService>>)
            .AddSingleton(dialogService)
            .AddScoped<TestWizard>();

        return dialogService;
    }

    public class RenderComponent : ClusterWizardDialogTests
    {

        [Fact]
        public async Task Component_should_get_rendered()
        {
            // Arrange
            await using var ctx = new BunitContext();
            SetupServices(ctx);
            ctx.Services.AddDialog();

            // Act
            var component = ctx.Render<ClusterWizardDialog>();

            // Assert
            component.Should().NotBeNull();
        }

        [Fact]
        public async Task Display_Dialog_set_false_on_cross_button_click()
        {
            // Arrange
            await using var ctx = new BunitContext();
            var dialogService = SetupServices(ctx);
            ctx.Services.AddDialog();
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act
            var component = ctx.Render<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var innerDialog = component.FindComponent<Dialog>();
            var sectionContent = innerDialog.RenderSectionContent(ctx);

            await sectionContent.FindAll("button")[1].ClickAsync();

            // Assert
            Assert.NotNull(component);
            dialogService.DisplayDialog.Should().BeFalse();
        }
    }

    public class OpenDialog : ClusterWizardDialogTests
    {
        [Fact]
        public async Task OpenDialog_should_set_current_wizard()
        {
            // Arrange
            await using var ctx = new BunitContext();
            var dialogService = SetupServices(ctx);
            ctx.Services.AddDialog();
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act
            var component = ctx.Render<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var innerDialog = component.FindComponent<Dialog>();
            var sectionContent = innerDialog.RenderSectionContent(ctx);

            // Assert
            sectionContent.Should().NotBeNull();
            dialogService.CurrentWizard.Should().BeOfType<TestWizard>();

            sectionContent.Find(".header")
                .InnerHtml.Should().Contain(TestWizard.Header);
        }

        [Fact]
        public async Task OpenDialog_should_render_current_wizard_component()
        {
            // Arrange
            await using var ctx = new BunitContext();
            var dialogService = SetupServices(ctx);
            ctx.Services.AddDialog();
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act
            var component = ctx.Render<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var innerDialog = component.FindComponent<Dialog>();
            var sectionContent = innerDialog.RenderSectionContent(ctx);

            // Assert
            sectionContent.Should().NotBeNull();
            var wizardContent = sectionContent.FindComponent<TestWizardComponent>();
            wizardContent.Should().NotBeNull();
            wizardContent.Nodes.Should().Satisfy(c => c.TextContent == TestWizardComponent.Content);
        }
    }

    public class ProcessCurrentWizard : ClusterWizardDialogTests
    {
        [Fact]
        public async Task Process_wizard_should_modify_builder()
        {
            // Arrange
            await using var ctx = new BunitContext();
            var dialogService = SetupServices(ctx);
            ctx.Services.AddDialog();
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act
            var component = ctx.Render<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var innerDialog = component.FindComponent<Dialog>();
            var sectionContent = innerDialog.RenderSectionContent(ctx);

            var changed = false;
            builder.ClusterChanged += (sender, e) =>
            {
                changed = true;
            };
            await sectionContent.FindAll("button").First(b => b.Id == ClusterWizardDialog.GenerateButtonId).ClickAsync();

            // Assert
            changed.Should().BeTrue();
        }

        [Fact]
        public async Task Cancel_wizard_should_leave_builder_unchanged()
        {
            // Arrange
            await using var ctx = new BunitContext();
            var dialogService = SetupServices(ctx);
            ctx.Services.AddDialog();
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act
            var component = ctx.Render<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var innerDialog = component.FindComponent<Dialog>();
            var sectionContent = innerDialog.RenderSectionContent(ctx);

            var changed = false;
            builder.ClusterChanged += (sender, e) =>
            {
                changed = true;
            };
            await sectionContent.FindAll("button").First(b => b.InnerHtml.Contains("Cancel", StringComparison.Ordinal)).ClickAsync();

            // Assert
            changed.Should().BeFalse();
        }
    }

    private class TestWizard : IClusterWizard
    {
        public const string Header = "TEST-MY-WIZARD";

        public string Title => Header;

        public Type ComponentType => typeof(TestWizardComponent);

        public Task PrepareWizardOptions(ClusterBuilder builder)
            => Task.CompletedTask;
        public Task ProcessWizardOptions(ClusterBuilder builder)
        {
            var dataflow = builder.Cluster.Dataflows.First();
            builder.Editors.Container.AddContainer(dataflow.Root, "TEST-WIZARD-CHANGE");

            return Task.CompletedTask;
        }
    }

    private class TestWizardComponent : IComponent
    {
        public const string Id = nameof(TestWizardComponent);
        public const string Css = "test-wizard-css";
        public const string Content = "test-wizard-content";

        public void Attach(RenderHandle renderHandle)
        {
            static void RenderFragment(RenderTreeBuilder builder)
            {
                builder.OpenElement(1, "label");
                builder.AddAttribute(2, "id", Id);
                builder.AddAttribute(3, "class", Css);
                builder.AddContent(4, Content);
                builder.CloseElement();
            }
            renderHandle.Render(RenderFragment);
        }
        public Task SetParametersAsync(ParameterView parameters) => Task.CompletedTask;
    }
}
