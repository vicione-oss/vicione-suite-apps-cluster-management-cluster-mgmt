using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.DebugPages;

public sealed partial class DebugEnginesPage : IEventConsumer<ClusterChanged>, IEventConsumer<EngineStarted>,
    IEventConsumer<EngineStopped>, IEventConsumer<EngineTornDown>
{
    private readonly string _startIconCssClass = MonochromeIconName.SendSolid.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _stopIconCssClass = MonochromeIconName.SwitchOff.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _trashIconCssClass = MonochromeIconName.Delete.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _infoIconCssClass = MonochromeIconName.InfoLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private string _clusterInfoText = "Cluster";
    private bool _hasLoaded;

    [Inject] private IUiMediator UiMediator { get; set; } = default!;
    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject] private IClusterService ClusterService { get; set; } = default!;
    [Inject] private NavigationManager NavManager { get; set; } = default!;

    private List<EngineInfoModel> Engines { get; } = [];
    private IReadOnlyDictionary<string, string> Packages { get; set; } = new Dictionary<string, string>();
    private bool _showPackages;


    protected override string PageTitle => "Debug Engines";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscriptionHandle.Add(UiMediator.Register<ClusterChanged>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineStarted>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineStopped>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineTornDown>(this));

        await ReloadEngines();

        _hasLoaded = true;
        await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeInternal()
    {
        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }
        
        _subscriptionHandle.Dispose();

        Engines.Clear();

        await base.DisposeInternal();
    }

    private async Task ReloadEngines()
    {
        var clusterInfos = await ManagementService.QueryClusterVersions(null, _tokenSource.Token);
        var activeClusterInfo = clusterInfos.First(c => c.IsActive);
        var cluster = await ClusterService.Query(activeClusterInfo.Id, activeClusterInfo.Version, _tokenSource.Token);

        _clusterInfoText = $"{cluster.Name} - v{cluster.Version}";
        Engines.Clear();

        foreach (var engineHost in cluster.GetAllEngineHosts())
        {
            foreach (var engine in engineHost.Engines)
            {
                try
                {
                    var instanceId = FindInstanceOfEngineHost(cluster, engineHost.Id);
                    var response =
                        await UiMediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
                            new(engine.Id), instanceId, _tokenSource.Token);

                    Engines.Add(new()
                    {
                        EngineHostId = engineHost.Id,
                        EngineId = engine.Id,
                        EngineName = engine.Name,
                        EngineHostName = engineHost.Name,
                        EngineState = response.EngineState,
                        InstanceId = instanceId,
                    });
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to get engine {EngineId} state on instance", engine.Id);
                }
            }
        }
    }

    public async Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
    {
        await ReloadEngines();
        await InvokeAsync(StateHasChanged);
    }

    public async Task Consume(ClientContext<EngineStarted> context, CancellationToken cancellationToken)
    {
        await ReloadEngines();
        await InvokeAsync(StateHasChanged);
    }

    public async Task Consume(ClientContext<EngineStopped> context, CancellationToken cancellationToken)
    {
        await ReloadEngines();
        await InvokeAsync(StateHasChanged);
    }

    public async Task Consume(ClientContext<EngineTornDown> context, CancellationToken cancellationToken)
    {
        await ReloadEngines();
        await InvokeAsync(StateHasChanged);
    }

    private async Task StartEngine(EngineInfoModel engineInfoModel)
        => await UiMediator.Send<StartEngineOnInstance>(new(engineInfoModel.EngineId), engineInfoModel.InstanceId);

    private async Task StopEngine(EngineInfoModel engineInfoModel)
        => await UiMediator.Send<StopEngineOnInstance>(new(engineInfoModel.EngineId), engineInfoModel.InstanceId);

    private async Task TearDownEngine(EngineInfoModel engineInfoModel)
        => await UiMediator.Send<TearDownEngineOnInstance>(new(engineInfoModel.EngineId), engineInfoModel.InstanceId);

    private async Task GetPackages(EngineInfoModel engineInfoModel)
    {
        var respond = await UiMediator.Request<GetPackagesOnInstanceRequest, GetPackagesOnInstanceResponse>(new(engineInfoModel.EngineId), engineInfoModel.InstanceId);

        if (respond != null && respond.RequestError is null && respond.Packages is not null)
        {
            Packages = respond.Packages;
            _showPackages = true;

            await InvokeAsync(StateHasChanged);
        }
    }

    private static Guid FindInstanceOfEngineHost(Cluster cluster, Guid engineHostId)
        => cluster.NodeGroups
               .SelectMany(x => x.Nodes)
               .SelectMany(x => x.Applications)
               .FirstOrDefault(a => a.EngineHosts.Any(eh => eh.Id == engineHostId))?.Id
           ?? throw new InvalidOperationException($"Could not find instance running engine host '{engineHostId}' in cluster '{cluster.Id}' {cluster.Version}.");

    private record EngineInfoModel
    {
        public Guid InstanceId { get; init; }
        public Guid EngineId { get; init; }
        public Guid EngineHostId { get; init; }
        public required string EngineName { get; init; }
        public required string EngineHostName { get; init; }
        public EngineState? EngineState { get; init; }
    }

    private void NavigateToEvents()
        => NavManager.NavigateToManagementEvents();

    private void NavigateToManagement()
        => NavManager.NavigateToManagement();
}
