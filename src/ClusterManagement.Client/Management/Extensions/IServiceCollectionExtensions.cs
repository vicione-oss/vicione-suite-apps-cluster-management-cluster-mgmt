using ClusterManagement.Client.Management.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Client.Management.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddClusterManagement(this IServiceCollection services)
    {
        services
            .AddScoped<IClusterManagementService, ClusterManagementPageService>();

        return services;
    }
}
