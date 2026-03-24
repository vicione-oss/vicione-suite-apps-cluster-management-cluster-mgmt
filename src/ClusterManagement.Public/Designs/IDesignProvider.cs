using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Public.Designs;

/// <summary>
/// Provides access to FunctionBlock design metadata and dependency resolution capabilities
/// for a given cluster configuration.
/// </summary>
public interface IDesignProvider
{
    /// <summary>
    /// Retrieves all cluster dependencies, optionally including data port dependencies.
    /// </summary>
    /// <param name="includeDataPortDependencies">
    /// <see langword="true"/> to include data port dependencies; otherwise, <see langword="false"/>.
    /// </param>
    Task<IReadOnlyCollection<ClusterDependency>> GetClusterDependencies(bool includeDataPortDependencies = false);

    /// <summary>
    /// Creates a resolver instance capable of resolving FunctionBlock dependencies.
    /// </summary>
    Task<IDependencyResolver> CreateResolver();

    /// <summary>
    /// Retrieves the FunctionBlock designs provided by the specified cluster dependencies.
    /// </summary>
    Task<IReadOnlyCollection<FunctionBlockDesign>> GetFunctionBlockDesigns(params IReadOnlyCollection<ClusterDependency> clusterDependencies);

    /// <summary>
    /// Retrieves the IDs of FunctionBlock designs provided by the specified cluster dependencies.
    /// </summary>
    /// <param name="clusterDependencies">
    /// The dependencies to filter by. If <see langword="null"/> or empty, all available design IDs will be returned.
    /// </param>
    Task<IReadOnlyCollection<Guid>> GetFunctionBlockDesignIds(params IReadOnlyCollection<ClusterDependency> clusterDependencies);
}
