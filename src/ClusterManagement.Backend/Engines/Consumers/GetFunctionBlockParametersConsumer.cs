using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class GetFunctionBlockParametersConsumer(IClusterDbContext clusterDb, IClusterSnapshotStore store, ISuiteMediator mediator)
    : RequestConsumer<GetFunctionBlockParametersRequest, GetFunctionBlockParametersResponse>
{
    protected override async Task<GetFunctionBlockParametersResponse> Respond(ConsumeContext<GetFunctionBlockParametersRequest> context)
    {
        var cluster = await GetClusterAsync(context.Message.ClusterId, context.CancellationToken);
        var functionBlocksPerApplications = GroupFunctionBlocksPerApplication(context, cluster);
        var result = await GetParametersFromApplications(functionBlocksPerApplications, context.CancellationToken);
        return result;
    }

    private async Task<Cluster> GetClusterAsync(Guid clusterId, CancellationToken cancellationToken)
    {
        var activeCluster = await clusterDb.ActiveClusters.SingleOrDefaultAsync(c => c.Id == clusterId, cancellationToken)
            ?? throw new ErrorInfoException(ClusterError.ClusterNotActive, $"Could not load active cluster '{clusterId}'.");

        return await store.Load(activeCluster.Id, activeCluster.Version, cancellationToken)
            ?? throw new ErrorInfoException(ClusterError.ClusterIdUnknown, $"Cannot find cluster '{clusterId}'.");
    }

    private static Dictionary<Guid, List<FunctionBlockParameterIds>> GroupFunctionBlocksPerApplication(ConsumeContext<GetFunctionBlockParametersRequest> context, Cluster cluster)
    {
        Dictionary<Guid, Guid> enginesPerApplications = [];
        foreach (var application in cluster.NodeGroups.SelectMany(g => g.Nodes).SelectMany(n => n.Applications))
        {
            foreach (var engine in application.EngineHosts.SelectMany(eh => eh.Engines))
                enginesPerApplications.Add(engine.Id, application.Id);
        }

        var functionBlocks = cluster.Dataflows.SelectMany(d => d.Root.FunctionBlocks).Where(fb => context.Message.FunctionBlockIds.Contains(fb.Id));

        return functionBlocks.Where(fb => fb.EngineId.HasValue)
            .GroupBy(fb => enginesPerApplications[fb.EngineId!.Value])
            .ToDictionary(g => g.Key, g => g.Select(fb => new FunctionBlockParameterIds(fb.Id,
                [.. Enumerable.Concat(fb.Settings.Select(s => s.Id), fb.PersistentVariables.Select(v => v.Id))])).ToList());
    }

    private async Task<GetFunctionBlockParametersResponse> GetParametersFromApplications(
        IDictionary<Guid, List<FunctionBlockParameterIds>> functionBlocksPerApplications, CancellationToken cancellationToken)
    {
        List<Task<RetrieveFunctionBlockParametersResponse>> tasks = [];
        foreach (var functionBlocksPerApplication in functionBlocksPerApplications)
        {
            RetrieveFunctionBlockParametersRequest retrieveParametersRequest = new(functionBlocksPerApplication.Value);
            var task = mediator.Request<RetrieveFunctionBlockParametersRequest, RetrieveFunctionBlockParametersResponse>
                (retrieveParametersRequest, functionBlocksPerApplication.Key, cancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        return new GetFunctionBlockParametersResponse([.. tasks.SelectMany(t => t.Result.FunctionBlockParameters)]);
    }

    protected override Task<GetFunctionBlockParametersResponse> HandleException(ConsumeContext<GetFunctionBlockParametersRequest> context, Exception e)
        => Task.FromResult(new GetFunctionBlockParametersResponse([])
        {
            RequestError = new(((ErrorInfoException)e).ErrorCode, e.Message),
        });
}
