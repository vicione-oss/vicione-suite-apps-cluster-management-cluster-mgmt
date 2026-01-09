using System.Reflection;
using ClusterManagement.Internal;

namespace ClusterManagement.Tests.Tools;

public static class ClusterManagementTools
{
    public static readonly ClusterManagementOptions ClusterManagementOptions = new()
    {
        MqttRpcTimeout = 10,
    };

    private static string GetFromEmbeddedResources(string resourceName)
    {
        var resourcePath = $"ClusterManagement.Tests.Tools.Resources.{resourceName}";
        var assembly = Assembly.GetAssembly(typeof(ClusterManagementTools));

        using var stream = assembly!.GetManifestResourceStream(resourcePath);
        using var reader = new StreamReader(stream ?? throw new InvalidOperationException($"EmbeddedResource {resourcePath} not found"));
        return reader.ReadToEnd();
    }

    public static string GetEmbeddedMqttRulesetYaml()
        => GetFromEmbeddedResources("Mqtt.yaml");
}
