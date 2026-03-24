using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;
using ViciOne.Cluster.Model.Extensions;
using ViciOne.Core.Contracts;
using ViciOne.Core.Contracts.DataModel.Extensions;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetDataflowEventsConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, IDesignProvider designProvider, ILogger<GetDataflowEventsConsumer> logger) :
    RequestConsumer<GetDataflowEventsRequest, GetDataflowEventsResponse>
{
    private readonly IClusterDbContext _dbContext = dbContext;
    private readonly IClusterSnapshotStore _store = store;
    private readonly IDesignProvider _designProvider = designProvider;
    private readonly ILogger<GetDataflowEventsConsumer> _logger = logger;

    public override async Task<GetDataflowEventsResponse> Respond(GetDataflowEventsRequest message, CancellationToken cancellationToken)
    {
        var activeVersion = await _dbContext.ActiveClusters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == message.ClusterId, cancellationToken)
            ?? throw new InvalidOperationException($"Cluster {message.ClusterId} is not active");

        var cluster = await _store.Load(activeVersion.Id, activeVersion.Version, cancellationToken);
        if (cluster is null)
        {
            return new GetDataflowEventsResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.NoClusterForVersion,
                        message.ClusterId, activeVersion.Version)
            };
        }

        var resolver = await _designProvider.CreateResolver();
        using var builder = new ClusterBuilder(cluster, resolver);

        List<IDataflowEvent>? dataflowEvents = null;

        if (message.DataflowId is not null)
        {
            var dataflow = builder.Cache.Dataflows.First(k => k.Id == message.DataflowId);
            var functionBlockIds = dataflow.Root.GetAllNestedFunctionBlocks().Select(k => k.Id);
            var connectors = builder.Cache.EventEnabledConnectors;
            var dataflowConnectors = connectors.Where(k => functionBlockIds.Contains(k.FunctionBlock.Id));

            dataflowEvents = GetAllEventEnabledConnectorEvents(dataflowConnectors, resolver, _logger);
        }
        else if (message.ClusterId is not null)
        {
            var connectors = builder.Cache.EventEnabledConnectors;

            dataflowEvents = GetAllEventEnabledConnectorEvents(connectors, resolver, _logger);
        }
        else
        {
            return new GetDataflowEventsResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.ClusterOrDataflowIdRequired,
                        message.ClusterId)
            };
        }

        return new GetDataflowEventsResponse
        {
            ClusterId = message.ClusterId ?? Guid.Empty,
            DataflowId = message.DataflowId ?? Guid.Empty,
            Results = dataflowEvents,
        };
    }

    private static List<IDataflowEvent> GetAllEventEnabledConnectorEvents(IEnumerable<Connector> connectors, IDependencyResolver dependencyResolver, ILogger logger)
    {
        var events = new List<IDataflowEvent>();

        foreach (var connector in connectors)
        {
            try
            {
                // Inputs
                if (connector is ConnectorInput input)
                {
                    events.Add(CreateDataflowEvent(dependencyResolver, input.FunctionBlock, input));
                }

                // Outputs
                else if (connector is ConnectorOutput output)
                {
                    events.Add(CreateDataflowEvent(dependencyResolver, output.FunctionBlock, output));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create dataflow events for FunctionBlock {Name} {Id}", connector.Name, connector.Id);
            }
        }

        return events;
    }

    private static DataflowEvent CreateDataflowEvent(IDependencyResolver dependencyResolver, FunctionBlock functionBlock, ConnectorInput input)
    {
        var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(functionBlock.DesignId);
        var connectorDesign = functionBlockDesign.GetInputs().First(x => x.Id == input.DesignId);

        return new DataflowEvent
        {
            CanWrite = true,
            EventId = input.Id,
            Name = $"{functionBlock.Name}.{input.Name}",
            ValueType = DataTypeCompatibilityValidator.DetermineValueType(connectorDesign.ConnectorType).AssemblyQualifiedName ?? string.Empty
        };
    }

    private static DataflowEvent CreateDataflowEvent(IDependencyResolver dependencyResolver, FunctionBlock functionBlock, ConnectorOutput output)
    {
        var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(functionBlock.DesignId);
        var connectorDesign = functionBlockDesign.GetOutputs().First(x => x.Id == output.DesignId);

        return new DataflowEvent
        {
            CanRead = true,
            EventId = output.Id,
            Name = $"{functionBlock.Name}.{output.Name}",
            ValueType = DataTypeCompatibilityValidator.DetermineValueType(connectorDesign.ConnectorType).AssemblyQualifiedName ?? string.Empty,
        };
    }

    public override Task<GetDataflowEventsResponse> HandleException(GetDataflowEventsRequest message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, "Failed to handle {Request}", nameof(GetClusterRequest));

        return Task.FromResult(new GetDataflowEventsResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, message.ClusterId),
        });
    }

    private class DataflowEvent : IDataflowEvent
    {
        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public Guid EventId { get; set; }

        public required string Name { get; set; }

        public required string ValueType { get; set; }
    }
}
