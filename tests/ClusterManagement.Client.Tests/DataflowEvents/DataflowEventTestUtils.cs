using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Tests.DataflowEvents;

internal static class DataflowEventTestUtils
{
    public static IDataflowEvent CreateInputEvent()
        => new TestDataflowEvent { EventId = Guid.NewGuid(), Name = "ConnectorInput", ValueType = "System.Bool", CanWrite = true };

    public static IDataflowEvent CreateOutputEvent()
        => new TestDataflowEvent { EventId = Guid.NewGuid(), Name = "ConnectorOutput", ValueType = "System.Bool", CanRead = true };


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

        public bool Equals(IDataflowEvent? other) => EventId.Equals(other?.EventId);
    }
}
