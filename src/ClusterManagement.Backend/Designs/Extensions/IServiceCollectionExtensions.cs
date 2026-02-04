using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDesignsSupport(this IServiceCollection services)
    {
        services
            .AddTransient<IClusterDependencyStore, ClusterDependencyStore>()
            .AddTransient<IDesignProvider, DesignProvider>()
            .AddTransient<IPackageArtifactRepository, PackageArtifactRepository>()
            .AddSingleton<IPackageContentProvider, PackageContentProvider>()
            .AddTransient<IPackageInfoProvider, PackageInfoProvider>()
            .AddTransient<IPackageStore, PackageStore>()
            .AddSingleton<ISuiteRulesetProvider, RulesetProvider>()
            .AddHostedService<DesignLoader>();

        return services;
    }
}
