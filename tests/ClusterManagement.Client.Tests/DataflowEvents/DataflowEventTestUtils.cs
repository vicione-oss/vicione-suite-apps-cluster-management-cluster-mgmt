using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Tests.DataflowEvents;

internal static class DataflowEventTestUtils
{
    public static IDataflowEvent CreateInputEvent(string functionBlockName = "Crest", string connectorName = "Threshold", params string[] containerPath)
        => new TestDataflowEvent
        {
            EventId = Guid.NewGuid(),
            Name = $"{functionBlockName}.{connectorName}",
            ValueType = "System.Double, System.Private.CoreLib",
            CanWrite = true,
            FunctionBlockName = functionBlockName,
            ConnectorName = connectorName,
            ContainerPath = containerPath,
        };

    public static IDataflowEvent CreateBooleanInputEvent(string functionBlockName, string connectorName)
    {
        var input = (TestDataflowEvent)CreateInputEvent(functionBlockName, connectorName);
        input.ValueType = "System.Boolean, System.Private.CoreLib";
        return input;
    }

    public static IDataflowEvent CreateOutputEvent(string functionBlockName = "Crest", string connectorName = "Value", params string[] containerPath)
        => new TestDataflowEvent
        {
            EventId = Guid.NewGuid(),
            Name = $"{functionBlockName}.{connectorName}",
            ValueType = "System.Double, System.Private.CoreLib",
            CanRead = true,
            FunctionBlockName = functionBlockName,
            ConnectorName = connectorName,
            ContainerPath = containerPath,
        };


    public static IUiMediator SetupDataflowEventRequest(this IUiMediator mediator, params IDataflowEvent[] clusterEvents)
    {
        mediator.Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(Arg.Any<GetDataflowEventsRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetDataflowEventsResponse()
                {
                    Results = [.. clusterEvents]
                });

        return mediator;
    }

    public sealed class TestDataflowEvent : IDataflowEvent
    {
        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public Guid EventId { get; set; }

        public required string Name { get; set; }

        public string? Unit { get; set; }

        public required string ValueType { get; set; }

        public Guid FunctionBlockId { get; set; } = Guid.NewGuid();

        public required string FunctionBlockName { get; set; }

        public required string ConnectorName { get; set; }

        public string DataflowName { get; set; } = "Line 3";

        public IReadOnlyList<string> ContainerPath { get; set; } = [];

        public bool Equals(IDataflowEvent? other) => EventId.Equals(other?.EventId);
    }
}
