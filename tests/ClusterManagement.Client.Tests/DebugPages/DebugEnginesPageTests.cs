using System.Globalization;
using Bunit;
using Bunit.TestDoubles;
using ClusterManagement.Client.DebugPages;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Messaging;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Components.ActionButtons;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.DebugPages;

public class DebugEnginesPageTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Guid s_deploymentId = Guid.NewGuid();
    private static readonly Version s_runningVersion = new(0, 1, 4);

    private readonly Guid _lineInstance = Guid.NewGuid();
    private readonly Guid _qualityInstance = Guid.NewGuid();
    private readonly Engine _filling = new() { Id = Guid.NewGuid(), Name = "Filling", RunIndex = 2, MinCycleTime = 100 };
    private readonly Engine _capping = new() { Id = Guid.NewGuid(), Name = "Capping", RunIndex = 1, MinCycleTime = 100 };
    private readonly Engine _vision = new() { Id = Guid.NewGuid(), Name = "Vision", RunIndex = 1, MinCycleTime = 100 };
    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly ExpertViewState _expertView = new();
    private IUiMediator _mediator = default!;
    private BunitAuthorizationContext _authorization = default!;

    [Fact]
    public async Task Groups_the_engines_under_their_engine_host_in_the_order_they_run()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        Rows(sut).Should().Equal("Line-Host", "Capping", "Filling", "Quality-Host", "Vision");
    }

    [Fact]
    public async Task Shows_each_engine_once_however_often_the_clusters_change_while_it_loads()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        await sut.InvokeAsync(() => Task.WhenAll(
            sut.Instance.Consume(new ClientContext<ClusterChanged>(ClusterChangedEvent(), Guid.NewGuid()), CancellationToken.None),
            sut.Instance.Consume(new ClientContext<ClusterChanged>(ClusterChangedEvent(), Guid.NewGuid()), CancellationToken.None)));

        // Assert
        sut.WaitForAssertion(() => Rows(sut).Should().Equal("Line-Host", "Capping", "Filling", "Quality-Host", "Vision"));
    }

    [Fact]
    public async Task Shows_each_state_as_it_arrives_while_another_host_does_not_answer()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var silent = new TaskCompletionSource<GetEngineStateOnInstanceResponse>();

        // Act
        var sut = RenderPage(ctx, setupStates: () =>
        {
            StateOf(_filling.Id).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
            StateOf(_capping.Id).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
            StateOf(_vision.Id).Returns(silent.Task);
        });

        // Assert
        sut.WaitForAssertion(() =>
        {
            StateText(sut, "Filling").Should().Be(ClusterOverview.EngineRunning);
            StateText(sut, "Vision").Should().Be("…");
        });
        silent.SetException(new TimeoutException());
    }

    [Fact]
    public async Task Asks_an_engine_again_once_it_started()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineStopped));
        StateOf(_vision.Id).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });

        // Act
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStarted>(new EngineStarted(_vision.Id), Guid.NewGuid()), CancellationToken.None));

        // Assert
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineRunning));
    }

    [Fact]
    public async Task Shows_the_engines_of_a_version_that_starts_to_run()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        var nextVersion = new Version(0, 1, 5);
        var next = Cluster();
        next.Version = nextVersion;
        next.NodeGroups[0].Nodes.RemoveAt(0);
        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>())
            .Returns([new ClusterInfoModel(new ClusterInfo(s_clusterId, nextVersion, "Cluster", IsActive: true))]);
        _clusterService.Query(s_clusterId, nextVersion, Arg.Any<CancellationToken>()).Returns(next);

        // Act
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<ClusterChanged>(ClusterChangedEvent(), Guid.NewGuid()), CancellationToken.None));

        // Assert
        sut.WaitForAssertion(() => Rows(sut).Should().Equal("Line-Host", "Capping", "Filling"));
    }

    [Fact]
    public async Task Opened_during_a_deployment_asks_no_engine_until_it_ends()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx, deploying: true);

        // Assert
        StateText(sut, "Vision").Should().Be(ClusterOverview.EngineDeploying);
        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Asks_every_engine_once_the_deployment_completed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, deploying: true, setupStates: () =>
            StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));

        // Act
        _managementService.OnClusterUpdateCompleted += Raise.Event<EventHandler<ClusterUpdateCompleted>>(_managementService,
            new ClusterUpdateCompleted(s_deploymentId, s_clusterId, s_runningVersion, TimeSpan.FromSeconds(3)));

        // Assert
        sut.WaitForAssertion(() => StateTexts(sut).Should().AllBe(ClusterOverview.EngineRunning));
    }

    [Fact]
    public async Task A_deployment_of_another_cluster_leaves_the_engines_alone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineStopped));

        // Act
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService,
            new ClusterUpdateStarted(Guid.NewGuid(), Guid.NewGuid(), new Version(1, 0, 0)));
        await sut.InvokeAsync(() => { });

        // Assert
        StateText(sut, "Vision").Should().Be(ClusterOverview.EngineStopped);
    }

    [Fact]
    public async Task Offers_no_engine_action_while_a_deployment_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx, engineControl: true, deploying: true);

        // Assert
        string[] engineActions = [CommonVocabulary.StartVerb, ClusterOverview.StopEngine, ClusterOverview.TearDownEngine];
        sut.FindComponents<GridActionButton>().Where(button => engineActions.Contains(button.Instance.Title))
            .Should().HaveCount(9).And.OnlyContain(button => !button.Instance.Enabled);
    }

    [Fact]
    public async Task Asks_every_engine_once_the_deployment_failed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, deploying: true, setupStates: () =>
            StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>()).Returns((DeploymentState?)null);

        // Act
        _managementService.OnClusterUpdateFailed += Raise.Event<EventHandler<ClusterUpdateFailed>>(_managementService,
            new ClusterUpdateFailed(s_deploymentId, s_clusterId, new Version(0, 1, 5)));

        // Assert
        sut.WaitForAssertion(() => StateTexts(sut).Should().AllBe(ClusterOverview.EngineRunning));
    }

    [Fact]
    public async Task Shows_the_engines_as_deploying_once_a_deployment_starts()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx);

        // Act
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService,
            new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, new Version(0, 1, 5)));

        // Assert
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineDeploying));
    }

    [Fact]
    public async Task Shows_an_engine_its_host_could_not_start()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx, setupStates: () =>
            StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.StartFailed }));

        // Assert
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineStartFailed));
    }

    [Fact]
    public async Task The_standard_view_shows_neither_ids_nor_actions()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx, engineControl: true);

        // Assert
        Headers(sut).Should().Equal(ClusterOverview.EngineColumn, CommonVocabulary.State, ClusterOverview.LatestCycle);
        sut.FindAll(".action-column").Should().BeEmpty();
    }

    [Fact]
    public async Task The_expert_view_adds_the_order_ids_function_blocks_log_level_and_actions()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        Headers(sut).Should().Equal(ClusterOverview.EngineColumn, ClusterOverview.RunIndexColumn, TechnicalTerms.Id,
            CommonVocabulary.State, ClusterOverview.LatestCycle, ClusterOverview.CycleRangeColumn, ClusterOverview.FunctionBlocks,
            ClusterOverview.LogLevelColumn, CommonVocabulary.ActionPlural);
        EngineRow(sut, "Vision").TextContent.Should().Contain(_vision.Id.ToString());
    }

    [Fact]
    public async Task The_standard_view_shows_each_host_with_its_summary_in_one_line()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        sut.FindAll("tr.host").Should().AllSatisfy(row => row.QuerySelectorAll("td").Should().ContainSingle()
            .Which.QuerySelector(".host-summary").Should().NotBeNull());
    }

    [Fact]
    public async Task The_expert_view_puts_the_id_of_each_host_in_the_column_of_the_engine_ids()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx);

        // Assert
        sut.FindAll("tr.host").Should().AllSatisfy(row =>
        {
            var cells = row.QuerySelectorAll("td");
            cells[0].GetAttribute("colspan").Should().Be("2");
            Guid.TryParse(cells[1].TextContent.Trim(), out _).Should().BeTrue();
        });
    }

    [Fact]
    public async Task The_expert_view_shows_the_fastest_and_slowest_cycle_and_since_when()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var deploymentId = Guid.NewGuid();
        var since = DateTimeOffset.UtcNow;
        var sut = RenderPage(ctx, setupStates: () => StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running, DeploymentId = deploymentId }));
        var series = ctx.Services.GetRequiredService<EngineCycleHub>().Series;

        // Act
        series.Add(Guid.NewGuid(), new EngineCycleReport(2, [new EngineCycleChainLink(deploymentId, 0.5)]), since);
        series.Add(Guid.NewGuid(), new EngineCycleReport(2, [new EngineCycleChainLink(deploymentId, 12.25)]), since.AddSeconds(1));

        // Assert
        var zone = ctx.Services.GetRequiredKeyedService<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey).LocalTimeZone;
        sut.WaitForAssertion(() =>
        {
            var cell = EngineRow(sut, "Vision").QuerySelector(".cycle-range")!;
            cell.TextContent.Trim().Should().Be($"{0.5.ToString("0.00", CultureInfo.CurrentCulture)} / {12.25.ToString("0.0", CultureInfo.CurrentCulture)} ms");
            cell.GetAttribute("title").Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.CycleRangeHint,
                DateTimeOffset.FromUnixTimeSeconds(since.ToUnixTimeSeconds()).To24HourTime(zone)));
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_expert_view_offers_only_the_packages_without_engine_control()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx, engineControl: false);

        // Assert
        ActionTitles(sut, "Vision").Should().Equal(ClusterOverview.ShowPackages);
    }

    [Fact]
    public async Task The_expert_view_offers_starting_stopping_and_tearing_down_with_engine_control()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx, engineControl: true);

        // Assert
        ActionTitles(sut, "Vision").Should().Equal(CommonVocabulary.StartVerb, ClusterOverview.StopEngine, ClusterOverview.TearDownEngine,
            ClusterOverview.ShowPackages);
    }

    [Fact]
    public async Task Offers_engine_control_before_the_engines_have_answered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();

        // Act
        var sut = RenderPage(ctx, engineControl: true, setupStates: () => StateOf().Returns(new TaskCompletionSource<GetEngineStateOnInstanceResponse>().Task));

        // Assert
        sut.WaitForAssertion(() => ActionTitles(sut, "Vision").Should().Contain(CommonVocabulary.StartVerb));
    }

    [Fact]
    public async Task Starting_starts_the_engine_on_its_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true);

        // Act
        await Action(sut, "Vision", CommonVocabulary.StartVerb).ClickAsync(new());

        // Assert
        await _mediator.Received(1).Send(Arg.Is<StartEngineOnInstance>(c => c.EngineId == _vision.Id), _qualityInstance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stopping_stops_the_engine_on_its_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true, setupStates: () =>
            StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineRunning));

        // Act
        await Action(sut, "Vision", ClusterOverview.StopEngine).ClickAsync(new());

        // Assert
        await _mediator.Received(1).Send(Arg.Is<StopEngineOnInstance>(c => c.EngineId == _vision.Id), _qualityInstance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_starts_nothing_once_the_right_is_gone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true);
        _authorization.SetPolicies();

        // Act
        await Action(sut, "Vision", CommonVocabulary.StartVerb).ClickAsync(new());

        // Assert
        await _mediator.DidNotReceive().Send(Arg.Any<StartEngineOnInstance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Switching_to_the_expert_view_shows_the_actions()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, engineControl: true);

        // Act
        await sut.InvokeAsync(_expertView.Toggle);

        // Assert
        sut.WaitForAssertion(() => ActionTitles(sut, "Vision").Should().Contain(CommonVocabulary.StartVerb));
    }

    [Fact]
    public async Task Tearing_down_asks_first()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true);

        // Act
        await Action(sut, "Vision", ClusterOverview.TearDownEngine).ClickAsync(new());

        // Assert
        DialogTitled(sut, ClusterOverview.TearDownEngine).Instance.Visible.Should().BeTrue();
        await _mediator.DidNotReceive().Send(Arg.Any<TearDownEngineOnInstance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirming_the_tear_down_tears_the_engine_down_on_its_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true);
        await Action(sut, "Vision", ClusterOverview.TearDownEngine).ClickAsync(new());

        // Act
        await DialogTitled(sut, ClusterOverview.TearDownEngine).RenderSectionContent(ctx)
            .FindFooterButton(b => b.Text == ClusterOverview.TearDownEngine).FindInnerButton().ClickAsync(new());

        // Assert
        await _mediator.Received(1).Send(Arg.Is<TearDownEngineOnInstance>(c => c.EngineId == _vision.Id), _qualityInstance, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirming_the_tear_down_of_an_engine_that_started_meanwhile_tears_nothing_down()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx, engineControl: true);
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineStopped));
        await Action(sut, "Vision", ClusterOverview.TearDownEngine).ClickAsync(new());
        StateOf(_vision.Id).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStarted>(new EngineStarted(_vision.Id), Guid.NewGuid()), CancellationToken.None));
        sut.WaitForAssertion(() => StateText(sut, "Vision").Should().Be(ClusterOverview.EngineRunning));

        // Act
        await DialogTitled(sut, ClusterOverview.TearDownEngine).RenderSectionContent(ctx)
            .FindFooterButton(b => b.Text == ClusterOverview.TearDownEngine).FindInnerButton().ClickAsync(new());

        // Assert
        await _mediator.DidNotReceive().Send(Arg.Any<TearDownEngineOnInstance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shows_the_packages_of_an_engine_sorted_by_name()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _expertView.Toggle();
        var sut = RenderPage(ctx);
        _mediator.Request<GetPackagesOnInstanceRequest, GetPackagesOnInstanceResponse>(Arg.Any<GetPackagesOnInstanceRequest>(), _qualityInstance, Arg.Any<CancellationToken>())
            .Returns(new GetPackagesOnInstanceResponse { Packages = new Dictionary<string, string> { ["Vision.Blocks"] = "1.2.0", ["Math"] = "2.0.1" } });

        // Act
        await Action(sut, "Vision", ClusterOverview.ShowPackages).ClickAsync(new());

        // Assert
        var dialog = DialogTitled(sut, string.Format(CultureInfo.CurrentCulture, ClusterOverview.PackagesOfEngine, "Vision"));
        dialog.Instance.Visible.Should().BeTrue();
        dialog.RenderSectionContent(ctx).FindAll(".packages tbody td:first-child").Select(c => c.TextContent).Should().Equal("Math", "Vision.Blocks");
    }

    private IRenderedComponent<DebugEnginesPage> RenderPage(BunitContext ctx, bool engineControl = false, Action? setupStates = null, bool deploying = false)
    {
        ctx.SetupSuiteServices(setup =>
        {
            _mediator = setup.ClientMediator;
            setup.Services.AddLocalization<ClusterManagementClientModule>();
        });
        _authorization = ctx.AddAuthorization();
        _authorization.SetAuthorized("operator");
        if (engineControl)
            _authorization.SetPolicies(EngineControlRight.Policy);

        ctx.AddClusterPageBar(_expertView);
        ctx.Services.AddDialog();
        ctx.Services.AddClientTimeProvider();
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton(_bannerService);
        AddCycleMonitor(ctx);

        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>())
            .Returns([new ClusterInfoModel(new ClusterInfo(s_clusterId, s_runningVersion, "Cluster", IsActive: true))]);
        _clusterService.Query(s_clusterId, s_runningVersion, Arg.Any<CancellationToken>()).Returns(_ => Cluster());
        if (deploying)
        {
            _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
                .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_runningVersion, new Version(0, 1, 5)) { CorrelationId = s_deploymentId });
        }
        if (setupStates is null)
            StateOf().Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Stopped });
        else
            setupStates();

        var page = ctx.Render<DebugEnginesPage>();
        page.WaitForAssertion(() => page.FindAll("tr.engine").Should().HaveCount(3));
        return page;
    }

    private static void AddCycleMonitor(BunitContext ctx)
    {
        var brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
        brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, EngineHostBroker>());
        ctx.Services.AddSingleton(_ => new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System,
            Substitute.For<ILogger<EngineCycleHub>>()));
        ctx.Services.AddSingleton(services => new EngineCycleMonitor(brokerProvider, services.GetRequiredService<EngineCycleHub>(),
            Substitute.For<ILogger<EngineCycleMonitor>>()));
    }

    // The state request of one engine, or of every engine without one.
    private Task<GetEngineStateOnInstanceResponse> StateOf(Guid? engineId = null)
        => _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
            Arg.Is<GetEngineStateOnInstanceRequest>(r => engineId == null || r.EngineId == engineId), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

    private static ClusterChanged ClusterChangedEvent()
        => new(Guid.NewGuid(), s_clusterId, CrudAction.Updated, s_runningVersion);

    // A host row shows the host's name, an engine row the engine's.
    private static IEnumerable<string> Rows(IRenderedComponent<DebugEnginesPage> sut)
        => sut.FindAll("tr.host .host-name, tr.engine .engine-name").Select(cell => cell.TextContent.Trim());

    private static IEnumerable<string> Headers(IRenderedComponent<DebugEnginesPage> sut)
        => sut.FindAll("thead th").Select(cell => cell.TextContent.Trim());

    private static AngleSharp.Dom.IElement EngineRow(IRenderedComponent<DebugEnginesPage> sut, string engine)
        => sut.FindAll("tr.engine").Single(row => row.QuerySelector(".engine-name")!.TextContent.Trim() == engine);

    private static string StateText(IRenderedComponent<DebugEnginesPage> sut, string engine)
        => EngineRow(sut, engine).QuerySelector(".state")!.TextContent.Trim();

    private static IEnumerable<string> StateTexts(IRenderedComponent<DebugEnginesPage> sut)
        => sut.FindAll("tr.engine .state").Select(state => state.TextContent.Trim());

    private static IEnumerable<string?> ActionTitles(IRenderedComponent<DebugEnginesPage> sut, string engine)
        => EngineRow(sut, engine).QuerySelectorAll(".action-column button").Select(button => button.GetAttribute("title"));

    private static AngleSharp.Dom.IElement Action(IRenderedComponent<DebugEnginesPage> sut, string engine, string title)
        => EngineRow(sut, engine).QuerySelectorAll(".action-column button").Single(button => button.GetAttribute("title") == title);

    private static IRenderedComponent<Dialog> DialogTitled(IRenderedComponent<DebugEnginesPage> sut, string title)
        => sut.FindComponents<Dialog>().Single(d => d.Instance.HeaderText == title);

    private Cluster Cluster()
        => new()
        {
            Id = s_clusterId,
            Version = s_runningVersion,
            Name = "Cluster",
            NodeGroups =
            [
                new ClusterNodeGroup
                {
                    Nodes =
                    [
                        Node(_qualityInstance, new EngineHost { Id = Guid.NewGuid(), Name = "Quality-Host", Engines = [_vision] }),
                        Node(_lineInstance, new EngineHost { Id = Guid.NewGuid(), Name = "Line-Host", Engines = [_filling, _capping] }),
                    ],
                },
            ],
        };

    private static ClusterNode Node(Guid instanceId, EngineHost host)
        => new()
        {
            Name = "edge",
            Applications = [new ClusterApplication { Id = instanceId, Name = "edge", Type = ClusterApplicationType.CoreOsStandalone, EngineHosts = [host] }],
        };
}
