using ClusterManagement.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Mqtt.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddMqttSupport(this IServiceCollection services,
        ClusterManagementOptions options)
    {
        services
            .AddSingleton<IMqttBrokerConnectionCache, MqttBrokerConnectionCache>()
            .Configure<MqttNetRpcClientOptions>(o => o.RequestTimeout = TimeSpan.FromSeconds(options.MqttRpcTimeout))
            .AddScoped<IMqttRpcClient, MqttNetRpcClient>()
            .AddScoped<IMqttObserverClient, MqttNetObserverClient>();

        return services;
    }
}
