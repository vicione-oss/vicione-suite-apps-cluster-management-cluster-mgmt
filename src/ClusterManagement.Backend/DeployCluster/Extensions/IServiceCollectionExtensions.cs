using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.DeployCluster.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDeploymentPlans(this IServiceCollection services)
        => services.AddSingleton<DeploymentPlanStore>();
}
