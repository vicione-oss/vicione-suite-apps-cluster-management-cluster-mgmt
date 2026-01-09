using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;

namespace ClusterManagement.Client.DebugEngines;

public sealed partial class DebugEnginesPage : IEventConsumer<ClusterChanged>, IEventConsumer<EngineStarted>, IEventConsumer<EngineStopped>, IEventConsumer<EngineTornDown>
{
    private bool _hasLoaded;

    [Inject] private IUiMediator UiMediator { get; set; } = default!;
    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject] private IClusterService ClusterService { get; set; } = default!;

    private List<EngineInfoModel> Engines { get; } = [];

    protected override string PageTitle => "Debug Engines";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await ReloadEngines();

        _hasLoaded = true;
    }

    private async Task ReloadEngines()
    {
        var clusterInfos = await ManagementService.QueryClusterVersions();
        var activeClusterInfo = clusterInfos.First(c => c.IsActive);
        var cluster = await ClusterService.Query(activeClusterInfo.Id, activeClusterInfo.Version);

        Engines.Clear();

        foreach (var engineHost in cluster.GetAllEngineHosts())
        {
            foreach (var engine in engineHost.Engines)
            {
                var instanceId = FindInstanceOfEngineHost(cluster, engineHost.Id);
                var response = await UiMediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(new(engine.Id), instanceId);

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
}
