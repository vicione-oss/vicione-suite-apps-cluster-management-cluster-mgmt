namespace ClusterManagement.Backend.Mqtt;

internal sealed record MqttNetRpcClientOptions
{
    internal TimeSpan RequestTimeout { get; set; }
}
