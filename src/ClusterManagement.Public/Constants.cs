using Sdk.Connections.Contracts;

namespace ClusterManagement.Public;

public static class Constants
{
    public const string EditorModuleId = "ClusterEditor";
    public const string ManagementModuleRoute = "/cluster-mgmt";
    public const string ManagementEventsRoute = ManagementModuleRoute + "-events";
    public const string EditorModuleRoute = "/cluster-editor";
    public const string EditorNavTileId = "cluster-editor";
    public const string DeployClusterState = "DeployClusterState";
    public const string EngineMonitoringTopicPostFix = "cycle";
    public const string DebugEnginesRoute = "/debug-engines";
    // these have to match yaml ruleset values!!
    public const string MqttRuleSetId = "MQTTDataPort";
    public const string MqttDataPortDesignId = "MQTT-Broker";
    public const string MqttDataPortTopicNodeId = "Folder";
    public const string DataPortCategory = "DataPorts";

    public const string PackagesContext = "vo-cluster-packages";

    public const string EngineHostProcessName = "ViciOne.ManagedEngine.Host";

    public const string EngineHostJournalFilterKey = "engine-host";
    public const string EngineHostJournalFilter = "SYSLOG_IDENTIFIER=ViciOne.ManagedEngine.Host+_SYSTEMD_UNIT=enginehost-privileged@*.service+_SYSTEMD_UNIT=enginehost@*.service";

    public static readonly Tag CentralBroker = new("Central Broker", new("30E624CE-6165-4D2D-AFCA-FC983BC0A559"))
    {
        Protected = true
    };

    public static readonly Version DefaultClusterVersion = new(0, 1, 0);
}
