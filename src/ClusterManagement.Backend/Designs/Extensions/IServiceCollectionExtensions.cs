using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDesignsSupport(this IServiceCollection services)
    {
        services
            .AddTransient<IPackageArtifactApi, PackageArtifactApi>()
            .AddHostedService<DesignLoader>()
            .AddTransient<IClusterDependencyStore, ClusterDependencyStore>()
            .AddSingleton<IPackagesStore, InMemoryPackagesStore>()
            .AddSingleton<ISuiteRulesetProvider>(s => (InMemoryPackagesStore)s.GetRequiredService<IPackagesStore>())
            .AddSingleton<IDesignProvider>(s => (InMemoryPackagesStore)s.GetRequiredService<IPackagesStore>());

        return services;
    }
}
