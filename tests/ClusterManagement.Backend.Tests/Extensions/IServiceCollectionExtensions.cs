using System.Diagnostics;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Modules;
using Sdk.Modules;
using Sdk.Testing.Backend;


namespace ClusterManagement.Backend.Tests.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection ConfigureDeployClusterSaga(this IServiceCollection services, IClusterSnapshotStore store, Action<IBusRegistrationConfigurator>? configure = null)
    {
        var moduleId = ModuleIdResolver.ResolveId<ClusterManagementBackendModule>();
        ClusterManagementOptions options = new();
        var config = new TestConfig().AddModuleWithOptions(moduleId, options);

        services.AddConfiguration(config)
            .AddSingleton(Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>())
            .AddSingleton(store)
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
            x.AddPublishMessageScheduler();

            configure?.Invoke(x);

            x.UsingInMemory((context, cfg) =>
            {
                cfg.UsePublishMessageScheduler();
                cfg.UseMessageRetry(x => x.None());

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
