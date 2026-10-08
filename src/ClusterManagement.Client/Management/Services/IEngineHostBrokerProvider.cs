namespace ClusterManagement.Client.Management.Services;

internal interface IEngineHostBrokerProvider
{
    /// <summary>
    /// The broker the engine hosts of each instance were deployed with. Instances without a broker are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, EngineHostBroker>> FindBrokers(IReadOnlyCollection<Guid> instanceIds, CancellationToken cancellationToken);
}
