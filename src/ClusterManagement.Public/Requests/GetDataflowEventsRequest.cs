using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetDataflowEventsRequest : IRequest<GetDataflowEventsResponse>
{
    /// <summary>
    /// specify the cluster you want to get events for
    /// null will take first available cluster
    /// </summary>
    public Guid? ClusterId { get; init; }

    /// <summary>
    /// set to get only events related to specified dataflow
    /// if null events of all dataflows will be requested
    /// </summary>
    public Guid? DataflowId { get; init; }
}
