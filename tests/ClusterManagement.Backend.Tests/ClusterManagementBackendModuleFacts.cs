using System.IO.Abstractions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Internal;
using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sdk.Backend.Artifacts;
using Sdk.Backend.IO;
using Sdk.Backend.Messaging;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.SystemConfiguration;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests;

public class ClusterManagementBackendModuleFacts
{
    public class RegisterServices
    {
        [Fact]
        public void Registers_instantiable_service()
        {
            var moduleId = ModuleIdResolver.ResolveId<ClusterManagementBackendModule>();
            ClusterManagementOptions options = new();
            var config = new TestConfig()
                .SetAppDirectory("temp")
                .AddModuleWithOptions(moduleId, options);

            var module = new ClusterManagementBackendModule();
            ServiceCollection serviceCollection = new();

            using var serviceProvider = module.TestModuleInitialization(
                services =>
                {
                    serviceCollection = services;
                    serviceCollection.AddConfiguration(config);
                    serviceCollection.AddOptions<ClusterManagementOptions>();
                    serviceCollection.AddSingleton(_ =>
                    {
                        var mvcBuilderMock = Substitute.For<IMvcBuilder>();
                        mvcBuilderMock.PartManager.Returns(Substitute.For<ApplicationPartManager>());
                        return mvcBuilderMock;
                    });
                    serviceCollection.AddSingleton(_ => Substitute.For<IFileSystem>());
                    serviceCollection.AddSingleton(_ => Substitute.For<IAtomicFileWriter>());
                    serviceCollection.AddSingleton(_ => Substitute.For<IArtifactRepository>());
                    serviceCollection.AddSingleton(_ => Substitute.For<IInstanceInformationProvider>());
                    serviceCollection.AddSingleton(_ => Substitute.For<ISuiteMediator>());
                    serviceCollection.AddSingleton(_ => Substitute.For<IControlServiceManagement>());
                    serviceCollection.AddSingleton(_ => Substitute.For<IConnectionTypeRegistry>());
                });

            IsInstantiable<IEngineHostPoolCommunication>(serviceCollection, serviceProvider);
            IsInstantiable<IEngineHostCommunication>(serviceCollection, serviceProvider);
            IsInstantiable<IEngineCommunication>(serviceCollection, serviceProvider);
            IsInstantiable<IEngineHostPool>(serviceCollection, serviceProvider);
            IsInstantiable<DesignLoader>(serviceCollection, serviceProvider);
            IsInstantiable<IEngineLifetime>(serviceCollection, serviceProvider);
            IsInstantiable<EngineLifetimeService>(serviceCollection, serviceProvider);

            static void IsInstantiable<TServiceType>(ServiceCollection serviceCollection, IServiceProvider serviceProvider)
            {
                var type = typeof(TServiceType);
                if (type.IsAssignableTo(typeof(IHostedService)))
                {
                    var hostedServices = serviceProvider.GetServices<IHostedService>();
                    hostedServices.Should().ContainSingle(s => s.GetType() == type);
                }
                else
                {
                    var lifetime = serviceCollection.Where(d => d.ServiceType == type)
                        .Select(d => d.Lifetime).Distinct().Single();
                    using var scope = serviceProvider.CreateScope();
                    var targetServiceProvider = lifetime == ServiceLifetime.Scoped ? scope.ServiceProvider : serviceProvider;
                    targetServiceProvider.Invoking(s => s.GetRequiredService(type)).Should().NotThrow();
                }
            }
        }
    }

    public class UseServices
    {
        [Fact]
        public void Registers_the_opc_ua_client_and_server_connection_types()
        {
            var registry = Substitute.For<IConnectionTypeRegistry>();
            var app = Substitute.For<IApplicationBuilder>();
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IConnectionTypeRegistry)).Returns(registry);
            app.ApplicationServices.Returns(serviceProvider);

            new ClusterManagementBackendModule().UseServices(app);

            registry.Received(1).Register<OpcUaClientConnection, DefaultJsonConnectionSerializer<OpcUaClientConnection>>(
                ConnectionType.OpcUaClient.Name,
                Arg.Any<Func<IConnection>>(),
                Arg.Any<DefaultJsonConnectionSerializer<OpcUaClientConnection>>(),
                null);
            registry.Received(1).Register<OpcUaServerConnection, DefaultJsonConnectionSerializer<OpcUaServerConnection>>(
                ConnectionType.OpcUaServer.Name,
                Arg.Any<Func<IConnection>>(),
                Arg.Any<DefaultJsonConnectionSerializer<OpcUaServerConnection>>(),
                null);
        }
    }
}
