namespace ClusterManagement.Public.Enums;

/// <summary>
/// A step an instance goes through while it updates its deployments.
/// </summary>
public enum ClusterNodeDeploymentStep
{
    /// <summary>Reads the target cluster, its designs, the broker connection and the current engine hosts.</summary>
    Prepare,

    /// <summary>Decides what has to happen to each engine host and engine.</summary>
    Plan,

    /// <summary>Compares the settings of an engine host with the ones the target version needs.</summary>
    CompareEngineHostSettings,

    /// <summary>Asks an engine host for the states of its engines.</summary>
    QueryEngineStates,

    DeployEngineHost,
    StartEngineHost,
    StopEngineHost,

    /// <summary>Writes changed settings to an engine host.</summary>
    UpdateEngineHost,

    TearDownEngineHost,
    StopEngine,
    TearDownEngine,
    DeployEngine,
    StartEngine,

    /// <summary>Deletes the retained broker messages of engines that are gone.</summary>
    DeleteRetainedMessages,
}
