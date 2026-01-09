using ClusterManagement.Public.ErrorHandling;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public static class ClusterErrorInfos
{
    public static ErrorInfo MqttConnectionMisconfigured()
        => new(ClusterErrors.MqttConnectionMisconfigured, "The cluster broker MQTT-connection was misconfigured");
    public static ErrorInfo DeploymentCancelled(Guid clusterApplicationId)
        => new(ClusterErrors.DeploymentCancelled, $"Deploying on application {clusterApplicationId} was canceled");
    public static ErrorInfo DataPortTransformationFailed()
        => new(ClusterErrors.DataPortTransformationFailed, $"Failed to translate data ports.");
    public static ErrorInfo CouldNotCreateDeployParameter()
        => new(ClusterErrors.CouldNotCreateDeployParameter, "Could not create deploy parameter");

    //Engine hosts:
    public static ErrorInfo CouldNotDeployEngineHost()
        => new(ClusterErrors.CouldNotDeployEngineHost, "Could not deploy engine host");
    public static ErrorInfo CouldNotTearDownEngineHost()
        => new(ClusterErrors.CouldNotTearDownEngineHost, "Could not tear down engine host");
    public static ErrorInfo CouldNotStartEngineHost()
        => new(ClusterErrors.CouldNotStartEngineHost, "Could not start engine host");
    public static ErrorInfo CouldNotStopEngineHost()
        => new(ClusterErrors.CouldNotStopEngineHost, "Could not stop engine host");
    public static ErrorInfo CouldNotSetAppSettingsOfEngineHost()
        => new(ClusterErrors.CouldNotSetAppSettingsOfEngineHost, "Could not set app settings of engine host");
    public static ErrorInfo CouldNotGetAppSettingsOfEngineHost()
        => new(ClusterErrors.CouldNotGetAppSettingsOfEngineHost, "Could not get app settings of engine host");
    public static ErrorInfo CouldNotGetRunStatesOfEngines()
        => new(ClusterErrors.CouldNotGetRunStatesOfEngines, "Could not get run states of engines.");

    //Engines:
    public static ErrorInfo CouldNotDeployEngine()
        => new(ClusterErrors.CouldNotDeployEngine, "Could not deploy engine in engine host");
    public static ErrorInfo CouldNotTearDownEngine()
        => new(ClusterErrors.CouldNotTearDownEngine, "Could not tear down engine in engine host");
    public static ErrorInfo CouldNotStartEngine()
        => new(ClusterErrors.CouldNotStartEngine, "Could not start engine in engine host");
    public static ErrorInfo CouldNotStopEngine()
        => new(ClusterErrors.CouldNotStopEngine, "Could not stop engine in engine host");
}
