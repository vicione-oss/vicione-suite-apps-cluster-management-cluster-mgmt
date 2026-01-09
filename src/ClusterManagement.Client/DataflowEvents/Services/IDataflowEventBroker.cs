using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;

namespace ClusterManagement.Client.DataflowEvents.Services;

public interface IDataflowEventBroker : IEventBroker
{
    Task<ClusterInfo?> GetCurrentClusterInfo();
}
