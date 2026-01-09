namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public enum EngineState
{
    Stopped,
    Starting,
    StartFailed,
    Running,
    Stopping,
}
