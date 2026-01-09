using System.IO.Abstractions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Engines.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model.Extensions;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class GetEngineLogConsumer : InstanceDependentRequestConsumer<GetEngineLogRequest, GetEngineLogResponse>
{
    private readonly IClusterDbContext _dbContext;
    private readonly IClusterSnapshotStore _store;
    private readonly IFileSystem _fileSystem;
    private readonly string _engineLogDirectory;

    public GetEngineLogConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider)
    {
        _dbContext = dbContext;
        _store = store;
        _fileSystem = fileSystem;
        _engineLogDirectory = _fileSystem.GetEnginesDirectory(workspaceProvider);
    }

    protected override async Task<GetEngineLogResponse> Respond(ConsumeContext<GetEngineLogRequest> context)
    {
        var engine = await FindEngine(context.Message.ClusterId, context.Message.EngineId, context.CancellationToken);
        var log = await RetrieveLog(engine, context.CancellationToken);
        return new GetEngineLogResponse(log);
    }

    protected override Task<GetEngineLogResponse> HandleException(ConsumeContext<GetEngineLogRequest> context, Exception e)
        => Task.FromResult(new GetEngineLogResponse(new ErrorInfo(((ErrorInfoException)e).ErrorCode, e.Message)));

    private async Task<ViciOne.Cluster.Model.Engine> FindEngine(Guid clusterId, Guid engineId, CancellationToken cancellationToken)
    {
        var activeCluster = await _dbContext.ActiveClusters.SingleOrDefaultAsync(c => c.Id == clusterId, cancellationToken)
            ?? throw new ErrorInfoException(ClusterError.ClusterNotActive, $"Could not find active cluster '{clusterId}'.");

        var cluster = await _store.Load(activeCluster.Id, activeCluster.Version, cancellationToken)
            ?? throw new ErrorInfoException(ClusterError.ClusterIdUnknown, $"Cannot find cluster '{clusterId}'.");

        var engine = cluster.GetAllEngines().SingleOrDefault(e => e.Id == engineId);

        return engine ?? throw new ErrorInfoException(ClusterError.EngineUnknown, $"Cannot find engine '{engineId}' in cluster '{clusterId}'.");
    }

    private async Task<IReadOnlyCollection<LogEntry>> RetrieveLog(ViciOne.Cluster.Model.Engine engine, CancellationToken cancellationToken)
    {
        if (!_fileSystem.Directory.Exists(_engineLogDirectory))
            throw new ErrorInfoException(ClusterError.LogUnattainable, $"Cannot find directory for engine log files.");

        var logFiles = _fileSystem.Directory.GetFiles(_engineLogDirectory, $"*{engine.Id}*.*", SearchOption.AllDirectories);
        return logFiles.Length == 0
            ? throw new ErrorInfoException(ClusterError.LogUnattainable, $"Cannot find log file for engine '{engine.Id}' ({engine.Name}).")
            : await SeriLogReader.ReadFile(_fileSystem, logFiles[0], cancellationToken);
    }
}
