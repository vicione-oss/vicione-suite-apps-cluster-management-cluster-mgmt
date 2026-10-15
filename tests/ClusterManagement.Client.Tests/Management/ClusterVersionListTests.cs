using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Requests;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Button;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterVersionListTests
{
    private readonly List<ClusterInfoModel> _deleted = [];
    private readonly List<ClusterInfoModel> _deployed = [];

    [Fact]
    public async Task Delete_asks_for_confirmation_before_deleting()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var sut = RenderList(ctx);

        // Act
        await DeleteButtonOf(sut, "0.1.2").FindInnerButton().ClickAsync(new());

        // Assert
        _deleted.Should().BeEmpty();
        DialogTitled(sut, ClusterOverview.DeleteVersion).Instance.Visible.Should().BeTrue();
    }

    [Fact]
    public async Task Confirming_the_dialog_deletes_the_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var sut = RenderList(ctx);
        await DeleteButtonOf(sut, "0.1.2").FindInnerButton().ClickAsync(new());

        // Act
        var dialogContent = DialogTitled(sut, ClusterOverview.DeleteVersion).RenderSectionContent(ctx);
        await dialogContent.FindFooterButton(b => b.Text == CommonVocabulary.Delete).FindInnerButton().ClickAsync(new());

        // Assert
        _deleted.Should().ContainSingle().Which.Version.Should().Be(new Version(0, 1, 2));
        DialogTitled(sut, ClusterOverview.DeleteVersion).Instance.Visible.Should().BeFalse();
    }

    [Theory]
    [InlineData("0.1.1")]
    [InlineData("0.1.0")]
    public async Task Running_and_default_versions_cannot_be_deleted(string version)
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var sut = RenderList(ctx);

        // Assert
        DeleteButtonOf(sut, version).Instance.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Long_lists_show_the_newest_versions_and_the_running_one()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var versions = Enumerable.Range(0, 20).Select(i => ClusterInfoModels.Version($"0.1.{20 - i}", isLatest: i == 0, isActive: i == 15)).ToList();

        // Act
        var sut = ctx.Render<ClusterVersionList>(p => p.Add(c => c.Versions, versions));

        // Assert
        sut.Instance.VisibleVersions.Should().HaveCount(9)
            .And.Contain(versions[15])
            .And.StartWith(versions.Take(8));
        sut.FindAll(".gap-row").Select(g => g.TextContent.Trim()).Should().Equal(
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.HiddenVersions, 7),
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.HiddenVersions, 4));
        sut.FindAll(".list-footer").Should().BeEmpty();
    }

    [Fact]
    public async Task Long_lists_keep_the_version_being_deployed_visible_and_mark_the_gap()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var versions = Enumerable.Range(0, 20).Select(i => ClusterInfoModels.Version($"0.1.{20 - i}", isLatest: i == 0, isActive: i == 1)).ToList();
        var deployment = DeploymentProgressModel.Start(
            new ClusterManagement.Public.Events.ClusterUpdateStarted(Guid.NewGuid(), ClusterInfoModels.ClusterId, versions[^1].Version), DateTimeOffset.UnixEpoch);

        // Act
        var sut = ctx.Render<ClusterVersionList>(p => p
            .Add(c => c.Versions, versions)
            .Add(c => c.Deployment, deployment));

        // Assert
        sut.Instance.VisibleVersions.Should().HaveCount(9).And.EndWith(versions[^1]);
        sut.Find(".gap-row").TextContent.Should().Contain(string.Format(CultureInfo.CurrentCulture, ClusterOverview.HiddenVersions, 11));
        sut.FindAll(".row").Last().TextContent.Should().Contain(ClusterOverview.VersionDeploying);
    }

    [Fact]
    public async Task Show_all_lists_every_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var versions = Enumerable.Range(0, 20).Select(i => ClusterInfoModels.Version($"0.1.{20 - i}")).ToList();
        var sut = ctx.Render<ClusterVersionList>(p => p.Add(c => c.Versions, versions));

        // Act
        await sut.Find(".gap-row button").ClickAsync(new());

        // Assert
        sut.Instance.VisibleVersions.Should().HaveCount(20);
        sut.FindAll(".gap-row").Should().BeEmpty();
        sut.Find(".list-footer button").TextContent.Trim().Should().Be(ClusterOverview.ShowFewer);
    }

    [Fact]
    public async Task The_delete_dialog_asks_about_the_version_and_says_it_cannot_be_undone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var sut = RenderList(ctx);

        // Act
        await DeleteButtonOf(sut, "0.1.2").FindInnerButton().ClickAsync(new());

        // Assert
        var dialogContent = DialogTitled(sut, ClusterOverview.DeleteVersion).RenderSectionContent(ctx);
        dialogContent.Find(".dialog-question").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeleteConfirmation, "Cluster", "0.1.2"));
        dialogContent.Find(".dialog-consequence").TextContent.Should().Be(ClusterOverview.DeleteConsequence);
    }

    [Fact]
    public async Task The_deploy_default_dialog_names_the_running_version_it_replaces()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var sut = RenderList(ctx);

        // Act
        await DeployDefaultButton(sut).FindInnerButton().ClickAsync(new());

        // Assert
        var dialogContent = DialogTitled(sut, ClusterOverview.DeployDefault).RenderSectionContent(ctx);
        dialogContent.Find(".dialog-question").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployDefaultConfirmation, "0.1.0"));
        dialogContent.Find(".dialog-consequence").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployDefaultConsequence, "0.1.1"));
    }

    [Fact]
    public async Task Deploy_default_deploys_the_default_version_of_the_running_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var otherCluster = new ClusterInfoModel(new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 0), "Other"));
        var sut = ctx.Render<ClusterVersionList>(p => p
            .Add(c => c.Versions, [otherCluster, ClusterInfoModels.Version("0.1.1", isActive: true, isLatest: true), ClusterInfoModels.Version("0.1.0")])
            .Add(c => c.OnDeploy, EventCallback.Factory.Create<ClusterInfoModel>(this, _deployed.Add)));
        await DeployDefaultButton(sut).FindInnerButton().ClickAsync(new());

        // Act
        var dialogContent = DialogTitled(sut, ClusterOverview.DeployDefault).RenderSectionContent(ctx);
        await dialogContent.FindFooterButton(b => b.Text == ClusterOverview.Deploy).FindInnerButton().ClickAsync(new());

        // Assert
        _deployed.Should().ContainSingle().Which.Id.Should().Be(ClusterInfoModels.ClusterId);
    }

    [Fact]
    public async Task Deploy_default_asks_first_and_then_deploys_version_zero_one_zero()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var sut = RenderList(ctx);
        await DeployDefaultButton(sut).FindInnerButton().ClickAsync(new());
        _deployed.Should().BeEmpty();

        // Act
        var dialogContent = DialogTitled(sut, ClusterOverview.DeployDefault).RenderSectionContent(ctx);
        await dialogContent.FindFooterButton(b => b.Text == ClusterOverview.Deploy).FindInnerButton().ClickAsync(new());

        // Assert
        _deployed.Should().ContainSingle().Which.Version.Should().Be(new Version(0, 1, 0));
    }

    [Fact]
    public async Task Deploy_default_is_disabled_while_the_default_cluster_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var sut = ctx.Render<ClusterVersionList>(p => p.Add(c => c.Versions, [ClusterInfoModels.Version("0.1.1", isLatest: true), ClusterInfoModels.Version("0.1.0", isActive: true)]));

        // Assert
        DeployDefaultButton(sut).Instance.Enabled.Should().BeFalse();
    }

    // Issue #368: a second cluster named like the running one is told apart from it and offers no draft
    [Fact]
    public async Task Versions_of_a_second_cluster_with_the_same_name_are_headed_by_their_cluster_and_not_a_draft()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();
        var otherClusterId = Guid.NewGuid();
        var versions = new List<ClusterInfoModel>
        {
            ClusterInfoModels.Version("0.1.24", isActive: true, isLatest: true),
            ClusterInfoModels.Version("0.1.11"),
            ClusterInfoModels.Version("0.1.0"),
            new(new ClusterInfo(otherClusterId, new Version(0, 1, 0), "Cluster")) { IsLatest = true },
        };

        // Act
        var sut = ctx.Render<ClusterVersionList>(p => p.Add(c => c.Versions, versions));

        // Assert
        sut.FindAll(".row .chip").Select(c => c.TextContent).Should().Equal(ClusterOverview.Running, CommonVocabulary.Default);
        sut.FindAll(".cluster-head").Select(h => h.GetAttribute("title")).Should().Equal(ClusterInfoModels.ClusterId.ToString(), otherClusterId.ToString());
        sut.FindAll(".rows > div").Select(r => r.ClassList.Contains("cluster-head")).Should().Equal(true, false, false, false, true, false);
        sut.FindAll(".row .name").Should().BeEmpty("the cluster head names the cluster");
    }

    [Fact]
    public async Task Versions_of_one_cluster_have_no_cluster_head()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddDialog();

        // Act
        var sut = RenderList(ctx);

        // Assert
        sut.FindAll(".cluster-head").Should().BeEmpty();
        sut.FindAll(".row .name").Select(n => n.TextContent).Should().AllBe("Cluster");
    }

    private static IRenderedComponent<Dialog> DialogTitled(IRenderedComponent<ClusterVersionList> sut, string title)
        => sut.FindComponents<Dialog>().Single(d => d.Instance.HeaderText == title);

    private static IRenderedComponent<Button> DeployDefaultButton(IRenderedComponent<ClusterVersionList> sut)
        => sut.FindComponents<Button>().Single(b => b.Instance.Text == ClusterOverview.DeployDefault);

    private IRenderedComponent<ClusterVersionList> RenderList(BunitContext ctx)
        => ctx.Render<ClusterVersionList>(p => p
            .Add(c => c.Versions, [ClusterInfoModels.Version("0.1.2", isLatest: true), ClusterInfoModels.Version("0.1.1", isActive: true), ClusterInfoModels.Version("0.1.0")])
            .Add(c => c.OnDelete, EventCallback.Factory.Create<ClusterInfoModel>(this, _deleted.Add))
            .Add(c => c.OnDeploy, EventCallback.Factory.Create<ClusterInfoModel>(this, _deployed.Add)));

    private static IRenderedComponent<Button> DeleteButtonOf(IRenderedComponent<ClusterVersionList> sut, string version)
    {
        var index = sut.Instance.Versions.ToList().FindIndex(v => v.Version == new Version(version));
        return sut.FindComponents<Button>().Where(b => b.Instance.Title == ClusterOverview.DeleteVersion).ElementAt(index);
    }
}
