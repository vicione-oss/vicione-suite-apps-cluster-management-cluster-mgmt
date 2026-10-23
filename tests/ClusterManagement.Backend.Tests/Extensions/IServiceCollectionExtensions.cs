using System.Diagnostics;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Modules;
using Sdk.Testing.Backend;


namespace ClusterManagement.Backend.Tests.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection ConfigureDeployClusterSaga(this IServiceCollection services, IClusterSnapshotStore store, Action<IBusRegistrationConfigurator>? configure = null, ClusterManagementOptions? options = null)
    {
        var moduleId = ModuleIdResolver.ResolveId<ClusterManagementBackendModule>();
        options ??= new();
        var config = new TestConfig().AddModuleWithOptions(moduleId, options);

        services.AddConfiguration(config)
            .AddSingleton(Options.Create(options))
            .AddSingleton(Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>())
            .AddSingleton(store)
            .AddSingleton<IClusterLock, ClusterLock>()
            .AddSingleton<IClusterDbContext>(svc =>
            {
                var connection = new SqliteConnection($"DataSource={TestDbContextFactory.DataSourceInMemory}");
                connection.Open();

                var dbContext = TestDbContextFactory.CreateSqliteContext<ClusterDbContextSqlite>(connection);
                dbContext.Database.EnsureCreated();

                return dbContext;
            });

        services.AddMassTransitTestHarness(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.SetTestTimeouts(testInactivityTimeout: Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(1));

            configure?.Invoke(x);

            // As the Suite does (ADR-004 D1): a consumer's publishes and sends only go out once it handled the message
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageScope(context);
                endpoint.UseInMemoryOutbox(context);
            });

            x.UsingInMemory((context, cfg) =>
            {
                cfg.UseMessageRetry(x => x.None());

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
