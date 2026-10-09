using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.DataflowEvents.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDataflowEvents(this IServiceCollection services)
    {
        services
            .AddScoped(provider => new DataflowEventPageService(
                provider.GetRequiredService<IDataflowEventBroker>(),
                provider.GetRequiredService<IMessageBannerService>(),
                TimeProvider.System,
                provider.GetRequiredService<ILogger<DataflowEventPageService>>()))
            .AddScoped<IDataflowEventMqttClientProvider, DataflowEventMqttClientProvider>()
            .AddScoped<IDataflowEventMqttHandler, DataflowEventMqttHandler>()
            .AddScoped<IDataflowEventBroker, DataflowEventBroker>()
            .AddScoped<IEventBroker>(s => s.GetRequiredService<IDataflowEventBroker>());

        return services;
    }
}
