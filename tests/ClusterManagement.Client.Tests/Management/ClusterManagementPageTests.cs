using System.Globalization;
using Bunit;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Button;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Tables.SimpleTable.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;
using Range = System.Range;

namespace ClusterManagement.Client.Tests.Management;

public sealed class ClusterManagementPageTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Version s_runningVersion = new(0, 1, 1);
    private static readonly Version s_draftVersion = new(0, 1, 2);

    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();

    [Fact]
    public async Task PageGetsRendered()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddLocalization<ClusterManagementClientModule>();
            setup.ClientMediator.Request<GetDeployClusterStateRequest, GetDeployClusterStateResponse>(Arg.Any<GetDeployClusterStateRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetDeployClusterStateResponse()
                {
                    State = new DeploymentState(Guid.NewGuid(), "Deployed", new Version(1, 0, 0), new Version(1, 1, 0))
                });
        });
        ctx.Services.AddSimpleTable();
        ctx.JSInterop.ConfigureSimpleTableJSInterop();
        ctx.Services.AddToolbar();
        ctx.Services.AddClientTimeProvider();
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(Substitute.For<IClusterService>());
        ctx.Services.AddSingleton(Substitute.For<IDesignProvider>());

        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>())
            .Returns(TestDataFactory.CreateClusterViewModels(Guid.NewGuid()));

        // Act
        var sut = ctx.Render<ClusterManagementPage>();

        // Assert
        Assert.NotNull(sut);
    }


    [Fact]
    public async Task Shows_a_notice_when_no_version_is_running()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var versions = TestDataFactory.CreateClusterViewModels(Guid.NewGuid());

        // Act
        var sut = RenderPage(ctx, versions);

        // Assert
        sut.Find(".notice").TextContent.Should().Be(ClusterOverview.NoActiveVersion);
        sut.FindAll(".version-list .row").Should().HaveCount(Math.Min(versions.Count, 8));
    }

    [Fact]
    public async Task Shows_a_notice_when_there_is_no_version()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = RenderPage(ctx, []);

        // Assert
        sut.Find(".notice").TextContent.Should().Be(ClusterOverview.NoVersions);
        sut.FindAll(".version-list").Should().BeEmpty();
    }

    [Fact]
    public async Task Shows_a_deployment_as_soon_as_it_starts()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService, new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_draftVersion));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeployingVersion, s_draftVersion)));
    }

    [Fact]
    public async Task Shows_the_step_an_instance_works_on()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());
        var correlationId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService,
            new ClusterUpdateStarted(correlationId, s_clusterId, s_draftVersion)
            {
                NodeApplications = new() { [nodeId] = instanceId },
                Nodes = new() { [nodeId] = "edge-1" },
                Applications = new() { [instanceId] = "CoreOS" },
            });

        // Act
        _managementService.OnClusterNodeDeploymentStepChanged += Raise.Event<EventHandler<ClusterNodeDeploymentStepChanged>>(_managementService,
            new ClusterNodeDeploymentStepChanged(correlationId, s_clusterId, instanceId, ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Started)
            {
                EngineName = "Line 1",
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .instance .summary .text").TextContent.Should().Be(Format(ClusterOverview.StepDeployEngine, "Line 1")));
    }

    [Fact]
    public async Task Names_an_engine_host_the_new_version_removes_from_the_running_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var correlationId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var removedHostId = Guid.NewGuid();
        var running = new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Id = nodeId, Name = "edge-1", Applications = [new ClusterApplication
            {
                Id = instanceId,
                Name = "CoreOS",
                Type = ClusterApplicationType.CoreOsMaster,
                EngineHosts = [new EngineHost { Id = removedHostId, Name = "Packaging" }],
            }] }] }],
        };
        ctx.AddAuthorization();
        var sut = RenderPage(ctx, RunningAndDraft(), runningCluster: running);
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService,
            new ClusterUpdateStarted(correlationId, s_clusterId, s_draftVersion)
            {
                NodeApplications = new() { [nodeId] = instanceId },
                Nodes = new() { [nodeId] = "edge-1" },
                Applications = new() { [instanceId] = "CoreOS" },
            });

        // Act
        _managementService.OnClusterNodeDeploymentStepChanged += Raise.Event<EventHandler<ClusterNodeDeploymentStepChanged>>(_managementService,
            new ClusterNodeDeploymentStepChanged(correlationId, s_clusterId, instanceId, ClusterNodeDeploymentStep.TearDownEngineHost, ClusterNodeDeploymentStepStatus.Started)
            {
                EngineHostId = removedHostId,
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .instance .summary .text").TextContent.Should().Be(Format(ClusterOverview.StepTearDownEngineHost, "Packaging")));
    }

    [Fact]
    public async Task Names_an_engine_host_the_rollback_removes_from_the_new_version()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var correlationId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var addedHostId = Guid.NewGuid();
        _managementService.QueryCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>()).Returns(new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Id = nodeId, Name = "edge-1", Applications = [new ClusterApplication
            {
                Id = instanceId,
                Name = "CoreOS",
                Type = ClusterApplicationType.CoreOsMaster,
                EngineHosts = [new EngineHost { Id = addedHostId, Name = "Packaging" }],
            }] }] }],
        });
        var sut = RenderPage(ctx, RunningAndDraft());
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService,
            new ClusterUpdateStarted(correlationId, s_clusterId, s_draftVersion)
            {
                NodeApplications = new() { [nodeId] = instanceId },
                Nodes = new() { [nodeId] = "edge-1" },
                Applications = new() { [instanceId] = "CoreOS" },
            });

        // Act
        _managementService.OnClusterNodeDeploymentStepChanged += Raise.Event<EventHandler<ClusterNodeDeploymentStepChanged>>(_managementService,
            new ClusterNodeDeploymentStepChanged(correlationId, s_clusterId, instanceId, ClusterNodeDeploymentStep.TearDownEngineHost, ClusterNodeDeploymentStepStatus.Started)
            {
                EngineHostId = addedHostId,
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .instance .summary .text").TextContent.Should().Be(Format(ClusterOverview.StepTearDownEngineHost, "Packaging")));
    }

    [Fact]
    public async Task Ignores_the_steps_of_another_deployment_of_a_resumed_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var instanceId = Guid.NewGuid();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_runningVersion, s_draftVersion) { CorrelationId = Guid.NewGuid() });
        _managementService.QueryCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>()).Returns(new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [new ClusterNode { Name = "edge-1", Applications = [new ClusterApplication
            {
                Id = instanceId,
                Name = "CoreOS",
                Type = ClusterApplicationType.CoreOsMaster,
            }] }] }],
        });
        var sut = RenderPage(ctx, RunningAndDraft());
        await sut.Find(".deployment .status-line button").ClickAsync(new());

        // Act
        _managementService.OnClusterNodeDeploymentStepChanged += Raise.Event<EventHandler<ClusterNodeDeploymentStepChanged>>(_managementService,
            new ClusterNodeDeploymentStepChanged(Guid.NewGuid(), s_clusterId, instanceId, ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started)
            {
                OccurredAt = DateTimeOffset.UnixEpoch,
            });

        // Assert
        sut.FindAll(".deployment .instance .steps-toggle").Should().BeEmpty();
    }

    [Fact]
    public async Task Shows_the_failure_of_a_deployment()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService, new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_draftVersion));

        // Act
        _managementService.OnClusterUpdateFailed += Raise.Event<EventHandler<ClusterUpdateFailed>>(_managementService, new ClusterUpdateFailed(Guid.NewGuid(), s_clusterId, s_draftVersion) { State = "UpdatingDeployments" });

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeploymentFailed, s_draftVersion)));
    }

    [Fact]
    public async Task Shows_the_failure_of_a_deployment_that_never_reported_a_start()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        _managementService.OnClusterUpdateFailed += Raise.Event<EventHandler<ClusterUpdateFailed>>(_managementService,
            new ClusterUpdateFailed(Guid.NewGuid(), s_clusterId, s_draftVersion) { State = "Initial", Reason = "Cluster v0.1.2 not found." });

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeploymentFailed, s_draftVersion));
            sut.Find(".deployment .status-line .muted").TextContent.Should().EndWith("Cluster v0.1.2 not found.");
        });
    }

    [Fact]
    public async Task Shows_a_deployment_that_starts_while_the_page_loads()
    {
        // Arrange
        await using var ctx = new BunitContext();
        EventHandler<ClusterUpdateStarted>? started = null;
        _managementService.When(s => s.OnClusterUpdateStarted += Arg.Any<EventHandler<ClusterUpdateStarted>>())
            .Do(call => started = call.Arg<EventHandler<ClusterUpdateStarted>>());
        var versions = RunningAndDraft();
        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started?.Invoke(_managementService, new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_draftVersion));
            return versions;
        });

        // Act
        var sut = RenderPage(ctx, versions, queryVersions: false);

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeployingVersion, s_draftVersion)));
    }

    [Fact]
    public async Task Hides_the_running_cluster_when_the_new_version_cannot_be_loaded()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());
        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>()).Returns(
        [
            new(new ClusterInfo(s_clusterId, s_draftVersion, "Cluster", IsActive: true)) { IsLatest = true },
            new(new ClusterInfo(s_clusterId, s_runningVersion, "Cluster")),
        ]);
        _managementService.QueryCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>())
            .Returns<Cluster>(_ => throw new InvalidOperationException("Failed to query cluster"));

        // Act
        _managementService.OnClusterUpdateCompleted += Raise.Event<EventHandler<ClusterUpdateCompleted>>(_managementService, new ClusterUpdateCompleted(Guid.NewGuid(), s_clusterId, s_draftVersion, TimeSpan.FromSeconds(3)));

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.FindAll(".side-column").Should().BeEmpty();
            sut.FindAll(".summary-tiles").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task Reports_a_failed_reload_after_a_deployment_to_the_renderer()
    {
        // Arrange
        await using var ctx = new BunitContext();
        RenderPage(ctx, RunningAndDraft());
        _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>())
            .Returns<List<ClusterInfoModel>>(_ => throw new TimeoutException("No answer"));

        // Act
        _managementService.OnClusterUpdateCompleted += Raise.Event<EventHandler<ClusterUpdateCompleted>>(_managementService, new ClusterUpdateCompleted(Guid.NewGuid(), s_clusterId, s_draftVersion, TimeSpan.FromSeconds(3)));

        // Assert
        var exception = await ctx.Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task Reloads_the_versions_when_a_deployment_completes()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService, new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_draftVersion));

        // Act
        _managementService.OnClusterUpdateCompleted += Raise.Event<EventHandler<ClusterUpdateCompleted>>(_managementService, new ClusterUpdateCompleted(Guid.NewGuid(), s_clusterId, s_draftVersion, TimeSpan.FromSeconds(3)));

        // Assert
        sut.WaitForAssertion(() => _managementService.Received(2).QueryClusterVersions(null, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task Warns_when_a_deployment_is_rejected()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        _managementService.OnClusterUpdateRejected += Raise.Event<EventHandler<ClusterUpdateRejected>>(_managementService, new ClusterUpdateRejected(Guid.NewGuid(), s_clusterId, s_runningVersion, s_draftVersion, new Version(0, 1, 3)));

        // Assert
        sut.WaitForAssertion(() => _bannerService.Received(1).ShowMessageBanner(MessageType.Warning, Format(ClusterOverview.DeploymentRejected, new Version(0, 1, 3), s_draftVersion)));
    }

    [Fact]
    public async Task Resumes_a_deployment_that_runs_while_the_page_opens()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_runningVersion, s_draftVersion));

        // Act
        var sut = RenderPage(ctx, RunningAndDraft());

        // Assert
        sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeployingVersion, s_draftVersion));
    }

    // Issue #368: the version being committed is not stored yet, so asking for it only fails
    [Fact]
    public async Task Does_not_query_the_target_of_a_deployment_resumed_while_committing()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "CommittingCluster", s_runningVersion, s_draftVersion));

        // Act
        var sut = RenderPage(ctx, RunningAndDraft());

        // Assert
        sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.DeployingVersion, s_draftVersion));
        await _managementService.DidNotReceive().QueryCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lists_the_instances_of_a_resumed_deployment()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_runningVersion, s_draftVersion));
        _managementService.QueryCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>()).Returns(new Cluster
        {
            NodeGroups =
            [
                new ClusterNodeGroup
                {
                    Nodes =
                    [
                        new ClusterNode
                        {
                            Name = "edge-1",
                            Applications = [new ClusterApplication { Id = Guid.NewGuid(), Name = "Master", Type = ClusterApplicationType.CoreOsMaster }],
                        },
                    ],
                },
            ],
        });

        // Act
        var sut = RenderPage(ctx, RunningAndDraft());

        // Assert
        sut.Find(".deployment .status-line .muted").TextContent.Should().Be(Format(ClusterOverview.InstancesUpdated, 0, 1));
    }

    [Fact]
    public async Task Deploy_in_the_header_deploys_the_draft()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        await HeaderButton(sut, b => b.CssClass == "cm-primary").FindInnerButton().ClickAsync(new());

        // Assert
        await _managementService.Received(1).DeployCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deploy_in_the_header_ignores_the_newest_version_of_another_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var otherClusterId = Guid.NewGuid();
        List<ClusterInfoModel> versions = [new(new ClusterInfo(otherClusterId, new Version(0, 9, 0), "Other")) { IsLatest = true }, .. RunningAndDraft()];
        var sut = RenderPage(ctx, versions);

        // Act
        await HeaderButton(sut, b => b.CssClass == "cm-primary").FindInnerButton().ClickAsync(new());

        // Assert
        await _managementService.Received(1).DeployCluster(s_clusterId, s_draftVersion, Arg.Any<CancellationToken>());
        await _managementService.DidNotReceive().DeployCluster(otherClusterId, Arg.Any<Version>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deployment_state_says_when_no_deployment_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        await HeaderButton(sut, b => b.Title == ClusterOverview.ShowSagaState).FindInnerButton().ClickAsync(new());

        // Assert
        var dialog = sut.FindComponents<Dialog>().Single(d => d.Instance.HeaderText == ClusterOverview.ShowSagaState);
        dialog.Instance.Visible.Should().BeTrue();
        dialog.RenderSectionContent(ctx).Find(".saga-headline").TextContent.Should().Be(ClusterOverview.NoSagaRunning);
    }

    [Fact]
    public async Task Reset_clears_the_deployment_of_every_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_runningVersion, s_draftVersion));
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        await HeaderButton(sut, b => b.Title == ClusterOverview.ResetSagaState).FindInnerButton().ClickAsync(new());

        // Assert
        await _managementService.Received(1).ResetSagaState(s_clusterId, Arg.Any<CancellationToken>());
        sut.Find(".deployment .headline").TextContent.Should().Be(Format(ClusterOverview.RunningVersionDeployed, s_runningVersion));
    }

    [Fact]
    public async Task Reset_shows_why_it_failed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.ResetSagaState(s_clusterId, Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Saga is locked")));
        var sut = RenderPage(ctx, RunningAndDraft());

        // Act
        await HeaderButton(sut, b => b.Title == ClusterOverview.ResetSagaState).FindInnerButton().ClickAsync(new());

        // Assert
        var dialog = sut.FindComponents<Dialog>().Single(d => d.Instance.HeaderText == ClusterOverview.ShowSagaState);
        dialog.Instance.Visible.Should().BeTrue();
        dialog.RenderSectionContent(ctx).Markup.Should().Contain("Saga is locked");
    }

    [Fact]
    public async Task Shows_the_start_of_a_deployment_in_the_users_time_zone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+05", TimeSpan.FromHours(5), "UTC+05", "UTC+05");
        var sut = RenderPage(ctx, RunningAndDraft(), timeZone, new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero));

        // Act
        _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService, new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_draftVersion));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".deployment .panel-head .muted").TextContent.Should().Contain("15:00:00"));
    }

    private static IRenderedComponent<Button> HeaderButton(IRenderedComponent<ClusterManagementPage> sut, Func<Button, bool> predicate)
        => sut.FindComponent<ActiveClusterHeader>().FindComponents<Button>().Single(b => predicate(b.Instance));

    private static string Format(string format, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);

    private static List<ClusterInfoModel> RunningAndDraft()
        => [
            new(new ClusterInfo(s_clusterId, s_draftVersion, "Cluster")) { IsLatest = true },
            new(new ClusterInfo(s_clusterId, s_runningVersion, "Cluster", IsActive: true)),
        ];

    private IRenderedComponent<ClusterManagementPage> RenderPage(BunitContext ctx, List<ClusterInfoModel> versions,
        TimeZoneInfo? timeZone = null, DateTimeOffset? now = null, bool queryVersions = true, Cluster? runningCluster = null)
    {
        ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        ctx.Services.AddDialog();
        ctx.Services.AddClientTimeProvider(timeZone, now);
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(_bannerService);
        AddRuntimeServices(ctx);
        if (queryVersions)
            _managementService.QueryClusterVersions(null, Arg.Any<CancellationToken>()).Returns(versions);

        _managementService.QueryCluster(s_clusterId, s_runningVersion, Arg.Any<CancellationToken>()).Returns(runningCluster ?? new Cluster());

        return ctx.Render<ClusterManagementPage>();
    }

    private static void AddRuntimeServices(BunitContext ctx)
    {
        var brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
        brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, EngineHostBroker>());
        ctx.Services.AddSingleton(_ => new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System,
            Substitute.For<ILogger<EngineCycleHub>>()));
        ctx.Services.AddSingleton(services => new EngineCycleMonitor(brokerProvider, services.GetRequiredService<EngineCycleHub>(),
            Substitute.For<ILogger<EngineCycleMonitor>>()));
        ctx.Services.AddSingleton(Substitute.For<IInstanceInformationProvider>());
        ctx.Services.AddSingleton(Substitute.For<IDataflowEventBroker>());
        ctx.Services.AddSingleton<DataflowEventPageService>();
    }

    [Fact]
    public void Cluster_view_models_should_be_sorted_by_id_and_version()
    {
        // Arrange
        var cluster1UniqueId = new Guid("A85FAEE1-18AF-4F78-B795-238921AD1313");
        var cluster2UniqueId = new Guid("D34E5F28-96A1-4DCE-AF29-A6B51E62132E");
        var cluster3UniqueId = new Guid("BA389BEF-9C84-4BA7-A9D0-FCFB1C2FDD0C");

        var list = TestDataFactory.CreateClusterViewModels(cluster1UniqueId, cluster2UniqueId, cluster3UniqueId);
        var clusterCount1 = list.Count(k => k.Id == cluster1UniqueId);
        var clusterCount2 = list.Count(k => k.Id == cluster2UniqueId);
        var clusterCount3 = list.Count(k => k.Id == cluster3UniqueId);

        // Act
        list.Sort();

        var cluster1Versions = list.Take(new Range(0, clusterCount1)).ToList();
        var cluster3Versions = list.Take(new Range(clusterCount1, clusterCount1 + clusterCount3));
        var cluster2Versions = list.Take(new Range(clusterCount1 + clusterCount3, clusterCount1 + clusterCount3 + clusterCount2));

        // Assert
        cluster1Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster1UniqueId));
        cluster2Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster2UniqueId));
        cluster3Versions.Should().AllSatisfy(c => c.Id.Should().Be(cluster3UniqueId));
        cluster1Versions.Should().BeInDescendingOrder(k => k.Version);
    }
}
