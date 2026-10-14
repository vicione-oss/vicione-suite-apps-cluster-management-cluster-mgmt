using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Connections.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddOpcUaConnectionSupport(this IServiceCollection services)
        => services
            .AddSingleton<IOpcUaCertificateStore, OpcUaCertificateStore>()
            .AddHostedService<OpcUaCertificateCleanupService>();
}
