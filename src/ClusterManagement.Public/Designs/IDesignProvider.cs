using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Public.Designs;

public interface IDesignProvider
{
    Task<IReadOnlyCollection<ClusterDependency>> GetClusterDependencies(bool includeDataPortDependencies = false);
    Task<IDependencyResolver> CreateResolver();
    Task<IReadOnlyCollection<FunctionBlockDesign>> GetFunctionBlockDesigns(params IReadOnlyCollection<ClusterDependency>? clusterDependencies);

    /// <summary>
    /// returns FunctionBlockDesign ids provided by given cluster dependencies
    /// </summary>
    /// <param name="clusterDependencies"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<Guid>> GetFunctionBlockDesignIds(params IReadOnlyCollection<ClusterDependency>? clusterDependencies);
}
