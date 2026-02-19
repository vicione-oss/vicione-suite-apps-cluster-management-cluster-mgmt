using System.IO.Abstractions;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.SystemConfiguration;

namespace ClusterManagement.Backend.EngineHosts.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddEngineHostSupport(this IServiceCollection services, ClusterManagementOptions options)
    {
        services
            .AddTransient<IEngineHostArtifactRepository, EngineHostArtifactRepository>()
            .AddHostedService<EngineHostLoader>();

        services.AddScoped<IEngineHostPoolCommunication>(s =>
            {
                var fileSystem = s.GetRequiredService<IFileSystem>();
                var workspaceProvider = s.GetRequiredService<IWorkspaceProvider<ClusterManagementBackendModule>>();

                return new ProcessEngineHostPoolCommunication(
                    fileSystem.GetEngineHostInstallDirectory(workspaceProvider, options),
                    fileSystem.GetDeploymentsPath(workspaceProvider),
                    fileSystem,
                    s.GetRequiredService<IMqttObserverClient>(),
                    s.GetRequiredService<IControlServiceManagement>());
            })
            .AddScoped<IEngineHostCommunication, MqttEngineHostCommunication>()
            .AddScoped<IEngineCommunication, MqttEngineCommunication>()
            .AddScoped<IEngineHostPool, EngineHostPool>(s =>
            {
                var fileSystem = s.GetRequiredService<IFileSystem>();
                var workspaceProvider = s.GetRequiredService<IWorkspaceProvider<ClusterManagementBackendModule>>();

                return new EngineHostPool(
                    fileSystem.GetEngineHostVersionsPath(workspaceProvider),
                    fileSystem.GetDeploymentsPath(workspaceProvider),
                    fileSystem);
            });

        services
            .AddTransient<IEngineLifetime, EngineLifetime>()
            .AddHostedService<EngineLifetimeService>()
            .AddModuleHostRequestHandler<ClusterManagementHostRequestHandler>();

        return services;
    }
}
