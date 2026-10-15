using System.IO.Abstractions;
using ClusterManagement.Backend.Connections;
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
using Sdk.Authorization;
using Sdk.Backend.Artifacts;
using Sdk.Backend.IO;
using Sdk.Backend.Messaging;
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
        public void Registers_the_engine_control_feature()
        {
            using var serviceProvider = InitializeModule(out _);

            serviceProvider.GetServices<IModuleFeature>().Should().ContainSingle(feature =>
                feature.ModuleId == ModuleIdResolver.ResolveId<ClusterManagementBackendModule>() && feature.Name == Public.Constants.EngineControlFeature);
        }

        [Fact]
        public void Registers_instantiable_service()
        {
            using var serviceProvider = InitializeModule(out var serviceCollection);

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

        private static ServiceProvider InitializeModule(out ServiceCollection serviceCollection)
        {
            var moduleId = ModuleIdResolver.ResolveId<ClusterManagementBackendModule>();
            ClusterManagementOptions options = new();
            var config = new TestConfig()
                .SetAppDirectory("temp")
                .AddModuleWithOptions(moduleId, options);

            var module = new ClusterManagementBackendModule();
            ServiceCollection initialized = new();

            var serviceProvider = module.TestModuleInitialization(
                services =>
                {
                    initialized = services;
                    initialized.AddConfiguration(config);
                    initialized.AddOptions<ClusterManagementOptions>();
                    initialized.AddSingleton(_ =>
                    {
                        var mvcBuilderMock = Substitute.For<IMvcBuilder>();
                        mvcBuilderMock.PartManager.Returns(Substitute.For<ApplicationPartManager>());
                        return mvcBuilderMock;
                    });
                    initialized.AddSingleton(_ => Substitute.For<IFileSystem>());
                    initialized.AddSingleton(_ => Substitute.For<IAtomicFileWriter>());
                    initialized.AddSingleton(_ => Substitute.For<IArtifactRepository>());
                    initialized.AddSingleton(_ => Substitute.For<IInstanceInformationProvider>());
                    initialized.AddSingleton(_ => Substitute.For<ISuiteMediator>());
                    initialized.AddSingleton(_ => Substitute.For<IControlServiceManagement>());
                    initialized.AddSingleton(_ => Substitute.For<IConnectionTypeRegistry>());
                    initialized.AddSingleton(_ => Substitute.For<IHostApplicationLifetime>());
                });

            serviceCollection = initialized;
            return serviceProvider;
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

            Func<IConnection>? createClientConnection = null;
            Func<IConnection>? createServerConnection = null;
            registry.Register<OpcUaClientConnection, OpcUaConnectionSerializer<OpcUaClientConnection>>(
                ConnectionType.OpcUaClient.Name,
                Arg.Do<Func<IConnection>>(factory => createClientConnection = factory),
                Arg.Any<OpcUaConnectionSerializer<OpcUaClientConnection>>(),
                null);
            registry.Register<OpcUaServerConnection, OpcUaConnectionSerializer<OpcUaServerConnection>>(
                ConnectionType.OpcUaServer.Name,
                Arg.Do<Func<IConnection>>(factory => createServerConnection = factory),
                Arg.Any<OpcUaConnectionSerializer<OpcUaServerConnection>>(),
                null);

            new ClusterManagementBackendModule().UseServices(app);

            createClientConnection.Should().NotBeNull();
            createClientConnection().Should().BeOfType<OpcUaClientConnection>()
                .Which.ApplicationCertificatesPath.Should().StartWith("ua-certs/");
            createServerConnection.Should().NotBeNull();
            createServerConnection().Should().BeOfType<OpcUaServerConnection>()
                .Which.ApplicationCertificatesPath.Should().StartWith("ua-certs/");
        }
    }
}
