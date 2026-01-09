using Bunit;
using ClusterManagement.Client.Editor.Dialogs;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Tests.Tools;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Client;
using ViciOne.Cluster.Builder;
using Xunit;

namespace ClusterManagement.Client.Tests.Editor.Dialogs;

public class ClusterWizardDialogTests
{
    private static ClusterWizardDialogService SetupServices(TestContext ctx)
    {
        var dialogService = new ClusterWizardDialogService(ctx.Services, Substitute.For<ILogger<ClusterWizardDialogService>>());

        ctx.SetupSuiteServices();
        ctx.SetupDevExpressBlazor();
        ctx.Services
            .AddSingleton(Substitute.For<ILogger<ClusterWizardDialogService>>)
            .AddSingleton(dialogService)
            .AddScoped<TestWizard>();

        return dialogService;
    }

    public class RenderComponent : ClusterWizardDialogTests
    {

        [Fact]
        public void Component_should_get_rendered()
        {
            // Arrange
            using var ctx = new TestContext();
            SetupServices(ctx);

            // Act
            var component = ctx.RenderComponent<ClusterWizardDialog>();

            // Assert
            component.Should().NotBeNull();
        }

        [Fact]
        public async Task Display_Dialog_set_false_on_cross_button_click()
        {
            // Arrange
            using var ctx = new TestContext();
            var dialogService = SetupServices(ctx);
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act            
            var component = ctx.RenderComponent<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            component.FindAll("button")[1].Click();

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
            using var ctx = new TestContext();
            var dialogService = SetupServices(ctx);
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act            
            var component = ctx.RenderComponent<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            // Assert            
            component.Should().NotBeNull();
            dialogService.CurrentWizard.Should().BeOfType<TestWizard>();

            component.Find(".header-bar")
                .InnerHtml.Should().Contain(TestWizard.Header);
        }

        [Fact]
        public async Task OpenDialog_should_render_current_wizard_component()
        {
            // Arrange
            using var ctx = new TestContext();
            var dialogService = SetupServices(ctx);
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act            
            var component = ctx.RenderComponent<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            // Assert
            component.Should().NotBeNull();
            var wizardContent = component.FindComponent<TestWizardComponent>();
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
            using var ctx = new TestContext();
            var dialogService = SetupServices(ctx);
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act            
            var component = ctx.RenderComponent<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var changed = false;
            builder.ClusterChanged += (sender, e) =>
            {
                changed = true;
            };
            component.FindAll("button").First(b => b.Id == ClusterWizardDialog.GenerateButtonId).Click();

            // Assert
            changed.Should().BeTrue();
        }

        [Fact]
        public async Task Cancel_wizard_should_leave_builder_unchanged()
        {
            // Arrange
            using var ctx = new TestContext();
            var dialogService = SetupServices(ctx);
            var cluster = ClusterDbContextTools.CreateCluster(Guid.NewGuid());
            using var builder = new ClusterBuilder(cluster, Substitute.For<IDependencyResolver>());

            // Act            
            var component = ctx.RenderComponent<ClusterWizardDialog>();
            await dialogService.OpenDialog<TestWizard>(builder);

            var changed = false;
            builder.ClusterChanged += (sender, e) =>
            {
                changed = true;
            };
            component.FindAll("button").First(b => b.InnerHtml == "Cancel").Click();

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
