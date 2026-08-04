using System.IO.Abstractions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Designs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Sdk.Backend.IO;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Designs.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddDesignsSupport : IServiceCollectionExtensionsTests
    {
        [Fact]
        public void Should_register_all_design_services()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Substitute.For<IArtifactRepository>());
            services.AddSingleton(Substitute.For<IFileSystem>());
            services.AddSingleton(Substitute.For<IAtomicFileWriter>());
            services.AddSingleton(Options.Create(new ClusterManagementOptions()));
            services.AddSingleton(Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>());

            // Act
            services.AddDesignsSupport();
            var provider = services.BuildServiceProvider();

            // Assert
            provider.GetService<IPackageArtifactRepository>().Should().NotBeNull();
            provider.GetService<IClusterDependencyStore>().Should().NotBeNull();
            provider.GetService<IPackageStore>().Should().NotBeNull();
            provider.GetService<IDesignProvider>().Should().NotBeNull();
            provider.GetService<IPackageContentProvider>().Should().NotBeNull();
            provider.GetService<ISuiteRulesetProvider>().Should().NotBeNull();
        }

        [Fact]
        public void Should_register_design_loader_as_hosted_service()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Substitute.For<IArtifactRepository>());
            services.AddSingleton(Substitute.For<IFileSystem>());
            services.AddSingleton(Substitute.For<IAtomicFileWriter>());
            services.AddSingleton(Options.Create(new ClusterManagementOptions()));
            services.AddSingleton(Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>());

            // Act
            services.AddDesignsSupport();
            var provider = services.BuildServiceProvider();

            // Assert
            var hostedServices = provider.GetServices<IHostedService>();
            hostedServices.Should().ContainItemsAssignableTo<DesignLoader>();
        }
    }
}
