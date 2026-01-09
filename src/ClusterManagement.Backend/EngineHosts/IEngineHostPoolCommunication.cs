using Semver;

namespace ClusterManagement.Backend.EngineHosts;

public interface IEngineHostPoolCommunication
{
    Task<IReadOnlyDictionary<Guid, EngineHostState>> GetStates(CancellationToken cancellationToken);
    Task<EngineHostState> GetState(Guid id, CancellationToken cancellationToken);
    Task DeployHost(Guid id, CancellationToken cancellationToken);
    Task TearDownHost(Guid id, CancellationToken cancellationToken);
    Task StartHost(Guid id, bool elevatedPrivileges, CancellationToken cancellationToken);
    Task StopHost(Guid id, CancellationToken cancellationToken);

    Task<int> CompareHostAppSettings(Guid id, EngineHostAppSettings appSettings, CancellationToken cancellationToken);
    Task SetHostAppSettings(Guid id, EngineHostAppSettings appSettings, CancellationToken cancellationToken);

    Task<SemVersion?> GetEngineHostVersion(CancellationToken cancellationToken, Guid? id = default);
    Task<SemVersion?> GetManagedEngineVersion(CancellationToken cancellationToken, Guid? engineHostId = default);
}
