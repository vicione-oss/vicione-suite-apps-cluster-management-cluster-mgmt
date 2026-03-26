using ClusterManagement.Backend.Engines;

namespace ClusterManagement.Backend.EngineHosts;

public interface IEngineHostCommunication
{
    /// <exception cref="InvalidOperationException">The engine can't be deployed by the engine host.</exception>
    Task<Guid> DeployEngine(Guid engineHostId, Workflow workflow, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">The engine can't be torn down by the engine host.</exception>
    Task TearDownEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">The engine can't be started by the engine host.</exception>
    Task<bool> StartEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">The engine can't be stopped by the engine host.</exception>
    Task<bool> StopEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">Engine state can't be obtained from the engine host.</exception>
    Task<EngineState> GetEngineState(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">Engines states can't be obtained from the engine host.</exception>
    Task<IReadOnlyDictionary<Guid, EngineState>> GetDeploymentStates(Guid engineHostId, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">Packages of deployment can't be obtained from the engine host.</exception>
    Task<IReadOnlyCollection<ClusterDependency>> GetPackages(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken);
}
