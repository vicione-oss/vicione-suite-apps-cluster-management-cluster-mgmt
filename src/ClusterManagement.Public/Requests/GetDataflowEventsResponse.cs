using ClusterManagement.Public.DataflowEvents;
using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetDataflowEventsResponse : IResponse
{
    public ErrorInfo? RequestError { get; init; }

    public Guid ClusterId { get; init; }
    public Guid? DataflowId { get; init; }
    public List<IDataflowEvent> Results { get; init; } = [];
}
