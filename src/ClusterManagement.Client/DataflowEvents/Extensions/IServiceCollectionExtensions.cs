using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Client.DataflowEvents.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddDataflowEvents(this IServiceCollection services)
    {
        services
            .AddScoped<DataflowEventPageService>()
            .AddScoped<IDataflowEventMqttClientProvider, DataflowEventMqttClientProvider>()
            .AddScoped<IDataflowEventMqttHandler, DataflowEventMqttHandler>()
            .AddScoped<IDataflowEventBroker, DataflowEventBroker>()
            .AddScoped<IEventBroker>(s => s.GetRequiredService<IDataflowEventBroker>());

        return services;
    }
}
