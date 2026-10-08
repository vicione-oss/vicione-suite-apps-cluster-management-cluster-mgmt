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

public sealed partial class GetDataflowEventsConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, IDesignProvider designProvider, ILogger<GetDataflowEventsConsumer> logger) :
    RequestConsumer<GetDataflowEventsRequest, GetDataflowEventsResponse>
{
    public override async Task<GetDataflowEventsResponse> Respond(GetDataflowEventsRequest message, CancellationToken cancellationToken)
    {
        var activeVersion = await dbContext.ActiveClusters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == message.ClusterId, cancellationToken)
            ?? throw new InvalidOperationException($"Cluster {message.ClusterId} is not active");

        var cluster = await store.Load(activeVersion.Id, activeVersion.Version, cancellationToken);
        if (cluster is null)
        {
            return new GetDataflowEventsResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.NoClusterForVersion,
                        message.ClusterId, activeVersion.Version)
            };
        }

        var resolver = await designProvider.CreateResolver();
        using var builder = new ClusterBuilder(cluster, resolver);

        List<IDataflowEvent>? dataflowEvents = null;

        if (message.DataflowId is not null)
        {
            var dataflow = builder.Cache.Dataflows.FirstOrDefault(k => k.Id == message.DataflowId)
                ?? throw new InvalidOperationException($"Dataflow {message.DataflowId} not found in cluster {message.ClusterId}");
            var functionBlockIds = dataflow.Root.GetAllNestedFunctionBlocks().Select(k => k.Id);
            var connectors = builder.Cache.EventEnabledConnectors;
            var dataflowConnectors = connectors.Where(k => functionBlockIds.Contains(k.FunctionBlock.Id));

            dataflowEvents = GetAllEventEnabledConnectorEvents(dataflowConnectors, builder.Cache.Dataflows, resolver, logger);
        }
        else if (message.ClusterId is not null)
        {
            var connectors = builder.Cache.EventEnabledConnectors;

            dataflowEvents = GetAllEventEnabledConnectorEvents(connectors, builder.Cache.Dataflows, resolver, logger);
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

    private static List<IDataflowEvent> GetAllEventEnabledConnectorEvents(IEnumerable<Connector> connectors, IReadOnlyCollection<Dataflow> dataflows,
        IDependencyResolver dependencyResolver, ILogger logger)
    {
        var events = new List<IDataflowEvent>();
        var dataflowNames = dataflows.ToDictionary(d => d.Root.Id, d => d.Name);

        foreach (var connector in connectors)
        {
            try
            {
                // Inputs
                if (connector is ConnectorInput input)
                {
                    events.Add(CreateDataflowEvent(dependencyResolver, input.FunctionBlock, input, LocationOf(input.FunctionBlock, dataflowNames)));
                }

                // Outputs
                else if (connector is ConnectorOutput output)
                {
                    events.Add(CreateDataflowEvent(dependencyResolver, output.FunctionBlock, output, LocationOf(output.FunctionBlock, dataflowNames)));
                }
            }
            catch (Exception ex)
            {
                LogCreateDataflowEventError(logger, ex, connector.Name, connector.Id);
            }
        }

        return events;
    }

    private static DataflowEvent CreateDataflowEvent(IDependencyResolver dependencyResolver, FunctionBlock functionBlock, ConnectorInput input,
        FunctionBlockLocation location)
    {
        var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(functionBlock.DesignId);
        var connectorDesign = functionBlockDesign.GetInputs().FirstOrDefault(x => x.Id == input.DesignId)
            ?? throw new InvalidOperationException($"Input connector design '{input.DesignId}' not found for function block '{functionBlock.Name}'");

        return new DataflowEvent
        {
            CanWrite = true,
            EventId = input.Id,
            Name = $"{functionBlock.Name}.{input.Name}",
            ValueType = DataTypeCompatibilityValidator.DetermineValueType(connectorDesign.ConnectorType).AssemblyQualifiedName ?? string.Empty,
            FunctionBlockId = functionBlock.Id,
            FunctionBlockName = functionBlock.Name,
            ConnectorName = input.Name,
            DataflowName = location.DataflowName,
            ContainerPath = location.ContainerPath,
        };
    }

    private static DataflowEvent CreateDataflowEvent(IDependencyResolver dependencyResolver, FunctionBlock functionBlock, ConnectorOutput output,
        FunctionBlockLocation location)
    {
        var functionBlockDesign = dependencyResolver.ResolveFunctionBlockDesign(functionBlock.DesignId);
        var connectorDesign = functionBlockDesign.GetOutputs().FirstOrDefault(x => x.Id == output.DesignId)
            ?? throw new InvalidOperationException($"Output connector design '{output.DesignId}' not found for function block '{functionBlock.Name}'");

        return new DataflowEvent
        {
            CanRead = true,
            EventId = output.Id,
            Name = $"{functionBlock.Name}.{output.Name}",
            ValueType = DataTypeCompatibilityValidator.DetermineValueType(connectorDesign.ConnectorType).AssemblyQualifiedName ?? string.Empty,
            FunctionBlockId = functionBlock.Id,
            FunctionBlockName = functionBlock.Name,
            ConnectorName = output.Name,
            DataflowName = location.DataflowName,
            ContainerPath = location.ContainerPath,
        };
    }

    // A function block's child containers lead up to the root container of its dataflow.
    private static FunctionBlockLocation LocationOf(FunctionBlock functionBlock, Dictionary<Guid, string> dataflowNames)
    {
        var containerPath = new List<string>();
        var container = functionBlock.Container;
        while (container is ChildContainer child)
        {
            containerPath.Insert(0, child.Name);
            container = child.Parent;
        }

        return new FunctionBlockLocation(dataflowNames.GetValueOrDefault(container.Id, container.Name), containerPath);
    }

    public override Task<GetDataflowEventsResponse> HandleException(GetDataflowEventsRequest message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogHandleException(e, nameof(GetClusterRequest));

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

        public required Guid FunctionBlockId { get; set; }

        public required string FunctionBlockName { get; set; }

        public required string ConnectorName { get; set; }

        public required string DataflowName { get; set; }

        public required IReadOnlyList<string> ContainerPath { get; set; }
    }

    private sealed record FunctionBlockLocation(string DataflowName, IReadOnlyList<string> ContainerPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create dataflow events for FunctionBlock {Name} {Id}")]
    private static partial void LogCreateDataflowEventError(ILogger logger, Exception exception, string name, Guid id);
}
