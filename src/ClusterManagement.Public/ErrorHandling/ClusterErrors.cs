namespace ClusterManagement.Public.ErrorHandling;

public static class ClusterErrors
{
    public const int MqttConnectionMisconfigured = 2;
    public const int DeploymentCancelled = 3;
    public const int DataPortTransformationFailed = 4;
    public const int CouldNotCreateDeployParameter = 9;

    public const int CouldNotDeployEngineHost = 100;
    public const int CouldNotTearDownEngineHost = 101;
    public const int CouldNotStartEngineHost = 102;
    public const int CouldNotStopEngineHost = 103;
    public const int CouldNotSetAppSettingsOfEngineHost = 106;
    public const int CouldNotGetAppSettingsOfEngineHost = 107;
    public const int CouldNotGetRunStatesOfEngines = 109;

    public const int CouldNotDeployEngine = 200;
    public const int CouldNotTearDownEngine = 201;
    public const int CouldNotStartEngine = 202;
    public const int CouldNotStopEngine = 203;
}
