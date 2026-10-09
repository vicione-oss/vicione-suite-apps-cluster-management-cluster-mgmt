using System.Globalization;
using Bunit;
using ClusterManagement.Client.DataflowEvents;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Button;
using ViciOne.Ui.Blazor.Components.CheckBox;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.SearchBox;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;
using ViciOne.Ui.Blazor.Components.TextBox;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;
using ViciOne.Ui.Localization.Resources;
using DataflowEventsText = ClusterManagement.Client.DataflowEvents.Localization.DataflowEventsPage;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public sealed class DataflowEventsPageTests
{
    private const string ClipboardWrite = "navigator.clipboard.writeText";
    private const string TreeKeysModule = "./_content/ViciOne.Suite.ClusterManagement.Client/DataflowEvents/DataflowEventsPage.razor.js";

    private readonly IDataflowEventBroker _eventBroker = Substitute.For<IDataflowEventBroker>().SubscribesEveryEvent();
    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();
    private readonly FakeTimeProvider _subscriptionClock = new();
    private BunitJSModuleInterop? _treeKeys;
    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent("Crest", "Threshold", "Pump 1");
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent("Axis", "Value", "Pump 2");
    private readonly DateTimeOffset _now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Page_should_render()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.SetupDataflowEventRequest(_inputEvent, _outputEvent);
            setup.Services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>();
        });
        ctx.Services.AddToolbar();
        ctx.Services.AddClientTimeProvider(now: _now);
        ctx.Services.AddSingleton(_eventBroker);
        ctx.Services.AddSingleton(_managementService);
        AddPageService(ctx);
        SetupTreeKeys(ctx);

        // Act
        var sut = ctx.Render<DataflowEventsPage>();

        // Assert
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task Names_the_running_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _eventBroker.GetCurrentClusterInfo().Returns(new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 2), "Cluster", IsActive: true));

        // Act
        var sut = RenderPage(ctx);

        // Assert
        sut.Find(".title .muted").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.DataflowEventsOfVersion, "0.1.2"));
    }

    [Fact]
    public async Task Reloads_the_events_after_a_deployment()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
#pragma warning disable xUnit1051 // The page service loads through the overload without a token, so that is the one to set up.
        _eventBroker.GetEvents().Returns([_inputEvent, _outputEvent, DataflowEventTestUtils.CreateOutputEvent("Scale", "Weight")]);
#pragma warning restore xUnit1051

        // Act
        _eventBroker.EventsChanged += Raise.Event<Func<Task>>();

        // Assert
        sut.WaitForAssertion(() => sut.Find(".shown").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.EventsShown, 3, 3)));
    }

    [Fact]
    public async Task Links_back_to_the_Cluster_Manager_with_the_running_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _eventBroker.GetCurrentClusterInfo().Returns(new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 2), "Cluster", IsActive: true));

        // Act
        var sut = RenderPage(ctx);

        // Assert
        sut.Find("a.cluster-status-link").GetAttribute("href").Should().Be(Constants.ManagementModuleRoute);
        sut.Find(".cluster-status-link .status").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.RunningVersionDeployed, "0.1.2"));
    }

    [Fact]
    public async Task Names_no_version_without_a_running_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        sut.FindAll(".title .muted").Should().BeEmpty();
    }

    [Fact]
    public async Task Events_are_grouped_by_function_block_after_their_containers_with_counts()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.GroupByFunctionBlock).FindInnerButton().ClickAsync(new());

        // Assert
        GroupToggles(sut).Select(b => b.Instance.Text).Should().Equal("Line 3 › Pump 1 › Crest", "Line 3 › Pump 2 › Axis");
        sut.Find(".group .group-counts").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.GroupCounts, 1, 0));
        sut.FindAll(".event .connector-name").Select(e => e.TextContent).Should().Equal("Crest.Threshold", "Axis.Value");
    }

    [Fact]
    public async Task The_connector_icon_tells_inputs_and_outputs_apart()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = await RenderExpandedPage(ctx);

        // Assert
        sut.FindAll(".event .connector-icon").Select(e => e.GetAttribute("title")).Should().Equal(
            string.Format(CultureInfo.CurrentCulture, DataflowEventsText.InputTitle, "Double"),
            string.Format(CultureInfo.CurrentCulture, DataflowEventsText.OutputTitle, "Double"));
    }

    [Fact]
    public async Task Without_grouping_the_events_are_listed_without_group_rows()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.GroupByNone).FindInnerButton().ClickAsync(new());

        // Assert
        sut.FindAll(".group").Should().BeEmpty();
        sut.FindAll(".event").Should().HaveCount(2);
    }

    [Fact]
    public async Task Showing_inputs_hides_the_outputs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.Inputs).FindInnerButton().ClickAsync(new());

        // Assert
        sut.FindAll(".event .connector-name").Select(e => e.TextContent).Should().Equal("Crest.Threshold");
    }

    [Fact]
    public async Task Collapsing_a_group_hides_its_events()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await Chip(sut, DataflowEventsText.GroupByFunctionBlock).FindInnerButton().ClickAsync(new());

        // Act
        await GroupToggles(sut).First().FindInnerButton().ClickAsync(new());

        // Assert
        sut.FindAll(".event .connector-name").Select(e => e.TextContent).Should().Equal("Axis.Value");
    }

    [Fact]
    public async Task A_click_anywhere_on_a_group_row_collapses_the_group()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await Chip(sut, DataflowEventsText.GroupByFunctionBlock).FindInnerButton().ClickAsync(new());

        // Act
        await sut.Find("tr.group .group-counts").ClickAsync(new());

        // Assert
        sut.FindAll(".event .connector-name").Select(e => e.TextContent).Should().Equal("Axis.Value");
    }

    [Fact]
    public async Task The_chevron_of_a_group_row_keeps_its_click_from_the_row()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        // bUnit passes no click on from an element with a handler of its own, so the place the browser stops it is checked.
        sut.FindAll("tr.group button").Should().ContainSingle()
            .Which.ParentElement!.HasAttribute("blazor:onclick:stoppropagation").Should().BeTrue();
    }

    [Fact]
    public async Task Grouping_by_input_and_output_names_both_groups()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.GroupByDirection).FindInnerButton().ClickAsync(new());

        // Assert
        GroupToggles(sut).Select(b => b.Instance.Text).Should().Equal(DataflowEventsText.InputsGroup, DataflowEventsText.OutputsGroup);
    }

    [Fact]
    public async Task Filter_shows_the_events_whose_name_or_location_contains_the_text()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await sut.InvokeAsync(() => sut.FindComponent<SearchBox>().Instance.TextChanging.InvokeAsync(" pump 2 "));

        // Assert
        sut.Find(".shown").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.EventsShown, 1, 2));
        sut.FindAll(".event .connector-name").Select(e => e.TextContent).Should().Equal("Axis.Value");
    }

    [Fact]
    public async Task The_container_tree_opens_with_its_dataflows_only()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var otherDataflowOutput = DataflowEventTestUtils.CreateOutputEvent("Scale", "Weight").InDataflow("Line 4");

        // Act
        var sut = RenderPage(ctx, _inputEvent, _outputEvent, otherDataflowOutput);

        // Assert
        Rows(sut).Should().Equal("Line 3", "Line 4");
    }

    [Fact]
    public async Task Expanding_a_node_shows_its_events_and_then_its_containers()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var dataflowOutput = DataflowEventTestUtils.CreateOutputEvent("Scale", "Weight");
        var sut = RenderPage(ctx, _inputEvent, _outputEvent, dataflowOutput);

        // Act
        await GroupToggles(sut).Single().FindInnerButton().ClickAsync(new());

        // Assert
        Rows(sut).Should().Equal("Line 3", "Scale.Weight", "Pump 1", "Pump 2");
    }

    [Fact]
    public async Task A_node_names_its_own_level_and_counts_everything_below_it()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await GroupToggles(sut).Single().FindInnerButton().ClickAsync(new());

        // Assert
        GroupToggles(sut).Single(b => b.Instance.Text == "Pump 1").Instance.Title.Should().Be("Line 3 › Pump 1");
        sut.Find(".group .group-counts").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.GroupCounts, 1, 1));
    }

    [Fact]
    public async Task Expand_all_opens_the_whole_tree()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await ExpandAll(sut);

        // Assert
        Rows(sut).Should().Equal("Line 3", "Pump 1", "Crest.Threshold", "Pump 2", "Axis.Value");
    }

    [Fact]
    public async Task Collapse_all_closes_the_whole_tree()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await ExpandAll(sut);

        // Act
        await sut.FindComponents<Button>().Single(b => b.Instance.Text == AllEvents(UserActions.CollapseSomething)).FindInnerButton().ClickAsync(new());

        // Assert
        Rows(sut).Should().Equal("Line 3");
    }

    [Fact]
    public async Task A_filter_opens_the_branches_with_a_match()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await sut.InvokeAsync(() => sut.FindComponent<SearchBox>().Instance.TextChanging.InvokeAsync("axis"));

        // Assert
        Rows(sut).Should().Equal("Line 3", "Pump 2", "Axis.Value");
    }

    [Fact]
    public async Task A_branch_the_filter_opened_can_be_closed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await sut.InvokeAsync(() => sut.FindComponent<SearchBox>().Instance.TextChanging.InvokeAsync("line 3"));

        // Act
        await GroupToggles(sut).Single(b => b.Instance.Text == "Pump 1").FindInnerButton().ClickAsync(new());

        // Assert
        Rows(sut).Should().Equal("Line 3", "Pump 1", "Pump 2", "Axis.Value");
    }

    [Fact]
    public async Task Changing_the_grouping_closes_the_tree_again()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await GroupToggles(sut).Single().FindInnerButton().ClickAsync(new());
        await Chip(sut, DataflowEventsText.GroupByDataflow).FindInnerButton().ClickAsync(new());

        // Act
        await Chip(sut, DataflowEventsText.GroupByContainer).FindInnerButton().ClickAsync(new());

        // Assert
        Rows(sut).Should().Equal("Line 3");
    }

    [Fact]
    public async Task Rows_tell_assistive_technology_their_place_in_the_tree()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var dataflowOutput = DataflowEventTestUtils.CreateOutputEvent("Scale", "Weight");
        var sut = RenderPage(ctx, _inputEvent, _outputEvent, dataflowOutput);

        // Act
        await GroupToggles(sut).Single().FindInnerButton().ClickAsync(new());

        // Assert
        sut.Find("table").GetAttribute("role").Should().Be("treegrid");
        sut.Find("table").GetAttribute("aria-label").Should().Be(DataflowEventsText.PageTitle);
        sut.FindAll("tr.group, tr.event").Select(TreePlace).Should().Equal("1 1/1 true", "2 1/3 ", "2 2/3 false", "2 3/3 false");
    }

    [Fact]
    public async Task Rows_tell_assistive_technology_how_many_there_are_and_where_each_one_sits()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = await RenderExpandedPage(ctx);

        // Assert
        sut.Find("table").GetAttribute("aria-rowcount").Should().Be("6");
        sut.FindAll("tr").Select(row => row.GetAttribute("aria-rowindex")).OfType<string>().Should().Equal("1", "2", "3", "4", "5", "6");
    }

    [Fact]
    public async Task Hands_the_table_to_the_arrow_keys_of_the_tree()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        RenderPage(ctx);

        // Assert
        _treeKeys!.VerifyInvoke("attach").Arguments.Should().ContainSingle().Which.Should().BeOfType<ElementReference>();
    }

    [Fact]
    public async Task Without_grouping_the_table_is_no_tree()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.GroupByNone).FindInnerButton().ClickAsync(new());

        // Assert
        sut.Find("table").HasAttribute("role").Should().BeFalse();
        sut.FindAll("tr.event").Should().OnlyContain(row => !row.HasAttribute("aria-level"));
    }

    [Fact]
    public async Task Collapse_all_closes_the_groups_the_filter_opened()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        await sut.InvokeAsync(() => sut.FindComponent<SearchBox>().Instance.TextChanging.InvokeAsync("line 3"));

        // Act
        await sut.FindComponents<Button>().Single(b => b.Instance.Text == AllEvents(UserActions.CollapseSomething)).FindInnerButton().ClickAsync(new());

        // Assert
        sut.FindAll(".event").Should().BeEmpty();
        sut.FindComponents<Button>().Should().Contain(b => b.Instance.Text == AllEvents(UserActions.ExpandSomething));
    }

    [Fact]
    public async Task Without_grouping_there_is_nothing_to_expand_or_collapse()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await Chip(sut, DataflowEventsText.GroupByNone).FindInnerButton().ClickAsync(new());

        // Assert
        sut.FindComponents<Button>().Should().NotContain(b =>
            b.Instance.Text == AllEvents(UserActions.CollapseSomething) || b.Instance.Text == AllEvents(UserActions.ExpandSomething));
    }

    [Fact]
    public async Task A_filter_without_matches_says_so()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await sut.InvokeAsync(() => sut.FindComponent<SearchBox>().Instance.TextChanging.InvokeAsync("xyzzy"));

        // Assert
        sut.Find(".empty").TextContent.Trim().Should().Be(DataflowEventsText.NoMatchingEvents);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    public async Task A_value_shows_its_date_only_when_it_is_not_from_today(int days, bool showsDate)
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);
        var onValue = await ValueHandler(sut, _outputEvent);

        // Act
        await sut.InvokeAsync(() => onValue(_now.AddDays(days).UtcDateTime, "1.5"));

        // Assert
        var expected = showsDate ? $"{_now.AddDays(days).ToString("d", CultureInfo.CurrentCulture)} 12:00:00" : "12:00:00";
        sut.WaitForAssertion(() => sut.Find(".event .time:not(:empty)").TextContent.Trim().Should().Be(expected));
    }

    [Fact]
    public async Task A_value_without_a_time_zone_is_read_as_utc()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"), _inputEvent, _outputEvent);
        var onValue = await ValueHandler(sut, _outputEvent);

        // Act
        await sut.InvokeAsync(() => onValue(new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Unspecified), "1.5"));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".event .time:not(:empty)").TextContent.Trim().Should().Be("12:00:00"));
    }

    [Fact]
    public async Task Values_arriving_together_are_rendered_together()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);
        var onValue = await ValueHandler(sut, _outputEvent);
        var rendersBeforeOne = sut.RenderCount;
        await sut.InvokeAsync(() => onValue(_now.UtcDateTime, "0"));
        sut.WaitForAssertion(() => sut.Find(".copy-cell .copy").TextContent.Should().Contain("0"));
        var rendersOfOne = sut.RenderCount - rendersBeforeOne;
        var rendersBeforeBurst = sut.RenderCount;

        // Act
        for (var i = 1; i <= 20; i++)
        {
            var value = i.ToString(CultureInfo.InvariantCulture);
            await sut.InvokeAsync(() => onValue(_now.UtcDateTime, value));
        }

        // Assert
        sut.WaitForAssertion(() => sut.Find(".copy-cell .copy").TextContent.Should().Contain("20"));
        (sut.RenderCount - rendersBeforeBurst).Should().BeLessThan(rendersOfOne * 3);
    }

    [Fact]
    public async Task A_value_without_timestamp_shows_no_time()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);
        var onValue = await ValueHandler(sut, _outputEvent);

        // Act
        await sut.InvokeAsync(() => onValue(default, "0"));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".copy-cell .copy").TextContent.Should().Contain("0"));
        sut.FindAll(".event .time").Should().OnlyContain(cell => cell.TextContent.Trim().Length == 0);
    }

    [Fact]
    public async Task An_input_field_is_labelled_with_its_connector()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = await RenderExpandedPage(ctx);

        // Assert
        var label = sut.Find(".write label");
        label.TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, DataflowEventsText.ValueFor, _inputEvent.Name));
        sut.Find($"input#{label.GetAttribute("for")}").Should().NotBeNull();
    }

    [Fact]
    public async Task Enter_writes_the_value_typed_in_its_row()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);
        await sut.InvokeAsync(() => ValueInput(sut).Instance.ValueChanging.InvokeAsync("3.5"));

        // Act
        await sut.InvokeAsync(() => ValueInput(sut).Instance.EnterPressed.InvokeAsync());

        // Assert
        await _eventBroker.Received(1).SetValue(_inputEvent.EventId, "3.5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_send_button_writes_the_value_typed_in_its_row()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);
        await sut.InvokeAsync(() => ValueInput(sut).Instance.ValueChanging.InvokeAsync("3.5"));

        // Act
        await WriteButton(sut).FindInnerButton().ClickAsync(new());

        // Assert
        await _eventBroker.Received(1).SetValue(_inputEvent.EventId, "3.5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_send_button_is_disabled_without_a_value()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = await RenderExpandedPage(ctx);

        // Assert
        WriteButton(sut).Instance.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Enter_on_an_empty_input_writes_nothing()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);

        // Act
        await sut.InvokeAsync(() => ValueInput(sut).Instance.EnterPressed.InvokeAsync());

        // Assert
        await _eventBroker.DidNotReceiveWithAnyArgs().SetValue(default, default, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_boolean_input_writes_its_value_when_the_checkbox_is_clicked()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var booleanInput = DataflowEventTestUtils.CreateBooleanInputEvent("Scan", "Trigger");
        var sut = await RenderExpandedPage(ctx, booleanInput, _outputEvent);

        // Act
        await sut.InvokeAsync(() => sut.FindComponent<CheckBox<bool>>().Instance.ValueChanged.InvokeAsync(true));

        // Assert
        await _eventBroker.Received(1).SetValue(booleanInput.EventId, bool.TrueString, Arg.Any<CancellationToken>());
        sut.FindComponents<TextBox>().Should().NotContain(t => t.Instance.CssClass == "value-input");
    }

    [Fact]
    public async Task Clicking_a_value_copies_it()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.JSInterop.SetupVoid(ClipboardWrite, _ => true).SetVoidResult();
        var sut = await RenderExpandedPage(ctx);
        var onValue = await ValueHandler(sut, _outputEvent);
        await sut.InvokeAsync(() => onValue(new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc), "2.91"));
        sut.WaitForElement(".copy-cell");

        // Act
        await sut.FindComponents<Button>().Single(b => b.Instance.CssClass == "copy").FindInnerButton().ClickAsync(new());

        // Assert
        ctx.JSInterop.VerifyInvoke(ClipboardWrite).Arguments.Should().Equal("2.91");
        sut.Find(".copy-cell .copied").TextContent.Should().Be(DataflowEventsText.Copied);
    }

    [Fact]
    public async Task Subscribes_the_outputs_it_shows()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = await RenderExpandedPage(ctx);

        // Act
        await ValueHandler(sut, _outputEvent);

        // Assert
        _eventBroker.SubscribeRequests().Should().ContainSingle().Which.Keys.Should().Equal(_outputEvent.EventId);
    }

    [Fact]
    public async Task Subscribes_no_outputs_below_a_closed_node()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        _subscriptionClock.Advance(ShownEventSubscriptions.SettleTime);
        await sut.InvokeAsync(() => { });

        // Assert
        _eventBroker.SubscribeRequests().Should().BeEmpty();
    }

    [Fact]
    public async Task A_boolean_input_keeps_its_checkbox_when_the_write_fails()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var booleanInput = DataflowEventTestUtils.CreateBooleanInputEvent("Scan", "Trigger");
        _eventBroker.SetValue(booleanInput.EventId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker unreachable")));
        var sut = await RenderExpandedPage(ctx, booleanInput, _outputEvent);
        var clicked = sut.FindComponent<CheckBox<bool>>().Instance;

        // Act
        await sut.InvokeAsync(() => clicked.ValueChanged.InvokeAsync(true));

        // Assert
        var shown = sut.FindComponent<CheckBox<bool>>().Instance;
        shown.Should().NotBeSameAs(clicked, "the browser keeps a clicked checkbox ticked unless it is rendered anew");
        shown.Value.Should().BeFalse();
    }

    private IRenderedComponent<DataflowEventsPage> RenderPage(BunitContext ctx)
        => RenderPage(ctx, _inputEvent, _outputEvent);

    private IRenderedComponent<DataflowEventsPage> RenderPage(BunitContext ctx, params IDataflowEvent[] events)
        => RenderPage(ctx, TimeZoneInfo.Utc, events);

    private IRenderedComponent<DataflowEventsPage> RenderPage(BunitContext ctx, TimeZoneInfo timeZone, params IDataflowEvent[] events)
    {
        ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>());
        ctx.Services.AddToolbar();
        ctx.Services.AddClientTimeProvider(timeZone, _now);
        ctx.Services.AddSingleton(_eventBroker);
        ctx.Services.AddSingleton(_managementService);
        AddPageService(ctx);
        SetupTreeKeys(ctx);
        ctx.Services.AddCheckBox();
        _eventBroker.GetEvents().Returns(events);

        return ctx.Render<DataflowEventsPage>();
    }

    private Task<IRenderedComponent<DataflowEventsPage>> RenderExpandedPage(BunitContext ctx)
        => RenderExpandedPage(ctx, _inputEvent, _outputEvent);

    private Task<IRenderedComponent<DataflowEventsPage>> RenderExpandedPage(BunitContext ctx, params IDataflowEvent[] events)
        => RenderExpandedPage(ctx, TimeZoneInfo.Utc, events);

    // The container tree opens with its dataflows only; the rows of the events show once it is expanded.
    private async Task<IRenderedComponent<DataflowEventsPage>> RenderExpandedPage(BunitContext ctx, TimeZoneInfo timeZone, params IDataflowEvent[] events)
    {
        var sut = RenderPage(ctx, timeZone, events);
        await ExpandAll(sut);
        return sut;
    }

    private static Task ExpandAll(IRenderedComponent<DataflowEventsPage> sut)
        => sut.FindComponents<Button>().Single(b => b.Instance.Text == AllEvents(UserActions.ExpandSomething)).FindInnerButton().ClickAsync(new());

    // A heading shows its name, an event its function block and connector.
    private static IEnumerable<string> Rows(IRenderedComponent<DataflowEventsPage> sut)
        => sut.FindAll("tr.group, tr.event").Select(row => row.ClassList.Contains("group")
            ? row.QuerySelector(".group-toggle")!.TextContent.Trim()
            : row.QuerySelector(".connector-name")!.TextContent);

    private static string TreePlace(AngleSharp.Dom.IElement row)
        => $"{row.GetAttribute("aria-level")} {row.GetAttribute("aria-posinset")}/{row.GetAttribute("aria-setsize")} {row.GetAttribute("aria-expanded")}";

    // The arrow keys of the tree run in the browser; the test only sees the table handed to them.
    private void SetupTreeKeys(BunitContext ctx)
    {
        _treeKeys = ctx.JSInterop.SetupModule(TreeKeysModule);
        _treeKeys.SetupVoid("attach", _ => true);
        _treeKeys.SetupVoid("detach", _ => true);
    }

    // The page's clock stands still at _now; the subscriptions run on one the test moves on.
    private void AddPageService(BunitContext ctx)
        => ctx.Services.AddSingleton(services => ActivatorUtilities.CreateInstance<DataflowEventPageService>(services, _subscriptionClock));

    // The page subscribes the outputs it shows once they have held for a moment; the subscription goes on on the
    // renderer's dispatcher.
    private async Task<Func<DateTime, string?, Task>> ValueHandler(IRenderedComponent<DataflowEventsPage> sut, IDataflowEvent dataflowEvent)
    {
        _subscriptionClock.Advance(ShownEventSubscriptions.SettleTime);
        await sut.InvokeAsync(() => { });

        return _eventBroker.SubscribeRequests().Single(handlers => handlers.ContainsKey(dataflowEvent.EventId))[dataflowEvent.EventId];
    }

    private IRenderedComponent<Button> WriteButton(IRenderedComponent<DataflowEventsPage> sut)
        => sut.FindComponents<Button>().Single(b => b.Instance.Title == string.Format(CultureInfo.CurrentCulture, DataflowEventsText.WriteValue, _inputEvent.Name));

    private static IRenderedComponent<TextBox> ValueInput(IRenderedComponent<DataflowEventsPage> sut)
        => sut.FindComponents<TextBox>().Single(t => t.Instance.CssClass == "value-input");

    private static IRenderedComponent<Button> Chip(IRenderedComponent<DataflowEventsPage> sut, string text)
        => sut.FindComponents<Button>().Single(b => b.Instance.Text == text && b.Instance.CssClass?.StartsWith("chip-toggle", StringComparison.Ordinal) == true);

    private static IEnumerable<IRenderedComponent<Button>> GroupToggles(IRenderedComponent<DataflowEventsPage> sut)
        => sut.FindComponents<Button>().Where(b => b.Instance.CssClass == "group-toggle");

    private static string AllEvents(string action)
        => string.Format(CultureInfo.CurrentCulture, action, CommonVocabulary.All);
}
