using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Snapshots.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddSnapshotStore(this IServiceCollection services)
    {
        services
            .AddTransient<IClusterSnapshotStore, ClusterSnapshotStore>()
            .AddTransient<ClusterSnapshotStoreMigration>();

        return services;
    }
}
