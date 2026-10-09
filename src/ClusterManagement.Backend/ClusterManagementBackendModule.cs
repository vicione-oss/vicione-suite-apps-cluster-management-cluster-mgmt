using ClusterManagement.Backend.Connections;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Iodds.Extensions;
using ClusterManagement.Backend.Mqtt.Extensions;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Instance;

namespace ClusterManagement.Backend;

public sealed class ClusterManagementBackendModule : BackendModule
{
    public override IModuleInitializer ModuleInitializer => new ClusterManagementModuleInitializer();

    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        services.AddModuleSection<ClusterManagementOptions>(this);

        var options = config.BindModuleSection<ClusterManagementOptions>(ModuleId);

        services
            .AddModuleDbContext<IClusterDbContext, ClusterDbContextSqlite, ClusterDbContextPostgres>(this,
                ClusterDbContext.DbSchemaName, enableSynchronization: false)
            // engine db context is not synchronized because it's only changed in deploy consumer on master|standalone
            .AddModuleDbContext<IEngineDbContext, EngineDbContextSqlite, EngineDbContextPostgres>(this,
                EngineDbContext.DbSchemaName, enableSynchronization: false)
            .AddModuleDbContext<IClusterSagaDbContext, ClusterSagaDbContextSqlite, ClusterSagaDbContextPostgres>(this,
                ClusterSagaDbContext.DbSchemaName, enableSynchronization: false)
            .AddModuleDbContext<IClusterEditorDbContext, ClusterEditorDbContextSqlite, ClusterEditorDbContextPostgres>(
                this,
                ClusterEditorDbContext.DbSchemaName, enableSynchronization: true);

        services
            .AddDesignsSupport()
            .AddSnapshotStore()
            .AddMqttSupport(options)
            .AddEngineHostSupport(options)
            .AddIoddSupport();

        services.AddHostedService<ResourceDownloadCompletedService>();
    }

    public override void UseServices(IApplicationBuilder app)
    {
        var registry = app.ApplicationServices.GetRequiredService<IConnectionTypeRegistry>();
        var opcUaConnectionFactory = ActivatorUtilities.CreateInstance<OpcUaConnectionFactory>(app.ApplicationServices);

        registry.Register<OpcUaClientConnection, DefaultJsonConnectionSerializer<OpcUaClientConnection>>(
            ConnectionType.OpcUaClient, opcUaConnectionFactory.CreateClientConnection, new(), null);
        registry.Register<OpcUaServerConnection, DefaultJsonConnectionSerializer<OpcUaServerConnection>>(
            ConnectionType.OpcUaServer, opcUaConnectionFactory.CreateServerConnection, new(), null);

        base.UseServices(app);
    }

    public override void ConfigureMessageBus(IServiceCollection busConfig, InstanceType instanceType)
    {
        // skip saga registration because only master or standalone are allowed to handle it
        if (instanceType == InstanceType.Slave)
            return;

        ((IBusRegistrationConfigurator)busConfig)
            .AddSagaStateMachine<DeployClusterStateMachine, DeployClusterState>(cfg => cfg.ConcurrentMessageLimit = 1)
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Optimistic; // requires RowVersion
                r.DatabaseFactory(s => () => (ClusterSagaDbContext)s.GetRequiredService<IClusterSagaDbContext>());
            });
    }
}
