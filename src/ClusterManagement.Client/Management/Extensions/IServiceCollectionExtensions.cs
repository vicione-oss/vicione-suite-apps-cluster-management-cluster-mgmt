using System.IO.Abstractions;
using ClusterManagement.Client.Management.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace ClusterManagement.Client.Management.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddClusterManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<IFileSystem, FileSystem>();
        services
            .AddScoped<IClusterManagementService, ClusterManagementPageService>()
            .AddScoped<IEngineHostBrokerProvider, EngineHostBrokerProvider>()
            .AddScoped<EngineCycleMonitor>()
            .AddScoped<ExpertViewState>()
            .AddSingleton(provider => new EngineCycleHub(
                () => new MqttClientFactory().CreateMqttClient(),
                TimeProvider.System,
                provider.GetRequiredService<ILogger<EngineCycleHub>>()))
            .AddSingleton(provider => new EngineCycleCollector(
                provider.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                provider.GetRequiredService<ILogger<EngineCycleCollector>>()));

        return services;
    }
}
