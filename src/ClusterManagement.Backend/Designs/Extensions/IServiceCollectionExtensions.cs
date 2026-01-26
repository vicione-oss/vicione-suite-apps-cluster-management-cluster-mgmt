using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDesignsSupport(this IServiceCollection services)
    {
        services
            .AddTransient<IPackageArtifactRepository, PackageArtifactRepository>()
            .AddHostedService<DesignLoader>()
            .AddTransient<IClusterDependencyStore, ClusterDependencyStore>()
            .AddTransient<IDesignProvider, DesignProvider>()
            .AddTransient<IPackageStore, PackageStore>()
            .AddSingleton<IPackageContentProvider, PackageContentProvider>()
            .AddSingleton<ISuiteRulesetProvider, RulesetProvider>();

        return services;
    }
}
