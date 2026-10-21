using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Snapshots.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddSnapshotStore(this IServiceCollection services)
    {
        services
            .AddTransient<IClusterSnapshotStore, ClusterSnapshotStore>()
            .AddSingleton<IClusterLock, ClusterLock>()
            .AddTransient<ClusterSnapshotStoreMigration>();

        return services;
    }
}
