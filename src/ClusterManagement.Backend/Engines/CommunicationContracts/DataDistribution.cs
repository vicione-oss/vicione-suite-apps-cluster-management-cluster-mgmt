namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class DataDistribution
{
    public List<ExternalConnection> ExternalConnections { get; init; } = [];
    public List<PersistenceConfiguration> SettingsPersistenceCommunications { get; init; } = [];
    public List<PersistenceConfiguration> VariablesPersistenceCommunications { get; init; } = [];
    public List<CommunicationConfiguration> RuntimeFunctionCommunications { get; init; } = [];
}
