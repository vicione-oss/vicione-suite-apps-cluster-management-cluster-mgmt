using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using ViciOne.Core.Contracts.DataModel;
using ViciOne.Core.Runtime.Attributes;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class DesignLoaderFacts
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IPackageArtifactApi _artifactApi = Substitute.For<IPackageArtifactApi>();
    private readonly IPackagesStore _packageStore = Substitute.For<IPackagesStore>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly ILogger<DesignLoader> _logger = Substitute.For<ILogger<DesignLoader>>();

    private readonly IServiceProvider _services;

    public DesignLoaderFacts()
    {
        _services = new ServiceCollection()
            .AddSingleton<ClusterManagementOptions>()
            .AddSingleton(_fileSystem)
            .AddSingleton(_workspaceService)
            .AddSingleton(_packageStore)
            .AddSingleton(_artifactApi)
            .AddTransient(_ => _logger)
            .BuildServiceProvider();
    }

    [Fact(Skip = "TEMP!")] //TODO: Make this work
    public async Task TestLoadDesigns()
    {
        // Arrange
        var shared = new List<Assembly>
        {
            typeof(FunctionBlockDesignAttribute).Assembly,
            typeof(IConnectorDesign).Assembly,
        };

        SetupSharedAssemblies<ClusterManagementBackendModule>(shared);

        var wsProvider = _services.GetRequiredService<IWorkspaceProvider<ClusterManagementBackendModule>>();
        var artifactApi = _services.GetRequiredService<IPackageArtifactApi>();
        var dependencyStore = Substitute.For<IClusterDependencyStore>();
        using var service = new DesignLoader(new FileSystem(), wsProvider, dependencyStore, _packageStore, artifactApi, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(service.LoadedFunctionBlockDesigns > 0);
        Assert.True(service.LoadedDataPortDesigns > 0);
    }

    private static void SetupSharedAssemblies<TModule>(IEnumerable<Assembly> moduleAssemblies) where TModule : BackendModule
    {
        var name = typeof(TModule).Assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException("Can't get name of TModule");

        var assemblyLoadContext = new AssemblyLoadContext(name);
        foreach (var moduleAssembly in moduleAssemblies)
        {
            assemblyLoadContext.LoadFromAssemblyName(moduleAssembly.GetName());
        }
    }
}

