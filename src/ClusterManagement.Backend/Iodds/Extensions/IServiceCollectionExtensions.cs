using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Iodds.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddIoddSupport(this IServiceCollection services)
    {
        services
            .AddSingleton<IIoddStore, IoddStore>()
            .AddHostedService<IoddService>();

        return services;
    }
}
