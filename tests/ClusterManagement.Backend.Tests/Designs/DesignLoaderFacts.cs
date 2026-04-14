using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;
using ViciOne.Core.Contracts.DataModel;
using ViciOne.Core.Runtime.Attributes;

namespace ClusterManagement.Backend.Tests.Designs;

public class DesignLoaderFacts
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IPackageArtifactRepository _repository = Substitute.For<IPackageArtifactRepository>();
    private readonly IPackageContentProvider _packageContentProvider = Substitute.For<IPackageContentProvider>();
    private readonly IPackageStore _packageStore = Substitute.For<IPackageStore>();
    private readonly ISuiteRulesetProvider _rulesetProvider = Substitute.For<ISuiteRulesetProvider>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly ILogger<DesignLoader> _logger = Substitute.For<ILogger<DesignLoader>>();
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();

    private readonly IServiceProvider _services;

    public DesignLoaderFacts()
    {
        _fileSystem.Path.Combine(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => $"{ci.ArgAt<string>(0)}/{ci.ArgAt<string>(1)}");

        // Default: no packages on disk
        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.deps.json", SearchOption.AllDirectories)
            .Returns([]);

        // Ensure the packages AssemblyLoadContext exists so ClusterSerializer.SetTypedSerializerOptions() won't throw
        if (!AssemblyLoadContext.All.Any(a => a.Name == Constants.PackagesContext))
            _ = new AssemblyLoadContext(Constants.PackagesContext);

        _services = new ServiceCollection()
            .AddSingleton<DesignLoader>()
            .AddSingleton<ClusterManagementOptions>()
            .AddSingleton(_fileSystem)
            .AddSingleton(_workspaceService)
            .AddSingleton(_packageStore)
            .AddSingleton(_repository)
            .AddSingleton(_packageContentProvider)
            .AddSingleton(_dependencyStore)
            .AddSingleton(_rulesetProvider)
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

        using var service = _services.GetRequiredService<DesignLoader>();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        Assert.True(service.LoadedFunctionBlockDesigns > 0);
        Assert.True(service.LoadedDataPortDesigns > 0);
    }

    public sealed class DownloadFailureTolerance : DesignLoaderFacts
    {
        private readonly ClusterDependency _systemDep = new() { Name = "ViciOne.Suite.System.DataPort", Version = "1.0.0" };

        public DownloadFailureTolerance()
        {
            _rulesetProvider.AddSystemDataPortFunctionBlock(Arg.Any<CancellationToken>())
                .Returns(_systemDep);
        }

        [Fact]
        public async Task Should_register_failed_downloads_via_AddFailedPackage()
        {
            // Arrange
            var dep1 = new ClusterDependency { Name = "ViciOne.Good", Version = "1.0.0" };
            var dep2 = new ClusterDependency { Name = "ViciOne.Bad", Version = "1.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep1, dep2]);

            _repository.DownloadAndExtract(dep1.Name, dep1.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/dep1", "target/dep1"));

            _repository.DownloadAndExtract(dep2.Name, dep2.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/dep2", "target/dep2") { Error = new InvalidOperationException("Artifact not found") });

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert
            _packageContentProvider.Received(1).AddFailedPackage(
                dep2,
                Arg.Is<ErrorInfo>(e => e.Message!.Contains("could not be downloaded")));
        }

        [Fact]
        public async Task Should_skip_failed_downloads_when_loading_packages()
        {
            // Arrange
            var dep1 = new ClusterDependency { Name = "ViciOne.Good", Version = "1.0.0" };
            var depFailed = new ClusterDependency { Name = "ViciOne.Bad", Version = "1.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep1, depFailed]);

            _repository.DownloadAndExtract(dep1.Name, dep1.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/dep1", "target/dep1"));

            _repository.DownloadAndExtract(depFailed.Name, depFailed.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/depFailed", "target/depFailed") { Error = new InvalidOperationException("fail") });

            SetupPackageOnDisk(dep1, "cache/Dependencies");

            _packageContentProvider.TryAddPackage(dep1, Arg.Any<string>(), false, Arg.Any<CancellationToken>())
                .Returns(true);

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert — dep1 should be loaded, depFailed should NOT be attempted via TryAddPackage
            await _packageContentProvider.Received(1).TryAddPackage(dep1, Arg.Any<string>(), false, Arg.Any<CancellationToken>());
            await _packageContentProvider.DidNotReceive().TryAddPackage(depFailed, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_register_missing_package_on_disk_as_failed()
        {
            // Arrange
            var dep = new ClusterDependency { Name = "ViciOne.Missing", Version = "1.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep]);

            _repository.DownloadAndExtract(dep.Name, dep.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/dep", "target/dep"));

            // Package downloaded but not found on disk (no .deps.json files)

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert
            _packageContentProvider.Received(1).AddFailedPackage(
                dep,
                Arg.Is<ErrorInfo>(e => e.Message!.Contains("can't be found on disk")));
        }

        [Fact]
        public async Task Should_complete_successfully_even_with_all_downloads_failed()
        {
            // Arrange
            var dep1 = new ClusterDependency { Name = "ViciOne.Pkg1", Version = "1.0.0" };
            var dep2 = new ClusterDependency { Name = "ViciOne.Pkg2", Version = "1.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep1, dep2]);

            _repository.DownloadAndExtract(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => new HttpLoaderResult($"source/{ci.ArgAt<string>(0)}", $"target/{ci.ArgAt<string>(0)}")
                { Error = new InvalidOperationException("fail") });

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert — should complete without throwing
            await service.Task;

            _packageContentProvider.Received(2).AddFailedPackage(Arg.Any<ClusterDependency>(), Arg.Any<ErrorInfo>());
        }

        [Fact]
        public async Task Should_log_error_when_downloads_fail()
        {
            // Arrange
            var dep = new ClusterDependency { Name = "ViciOne.Broken", Version = "2.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep]);

            _repository.DownloadAndExtract(dep.Name, dep.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/broken", "target/broken") { Error = new InvalidOperationException("fail") });

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert — verify error was logged for broken cluster state
            _logger.ReceivedWithAnyArgs().LogError(default, default, null, []);
        }

        [Fact]
        public async Task Should_not_throw_on_empty_dependencies()
        {
            // Arrange
            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ClusterDependency>());

            using var service = _services.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            await service.Task;
            _packageContentProvider.DidNotReceive().AddFailedPackage(Arg.Any<ClusterDependency>(), Arg.Any<ErrorInfo>());
        }
    }

    public sealed class LoggingBehavior : DesignLoaderFacts
    {
        private readonly ClusterDependency _systemDep = new() { Name = "ViciOne.Suite.System.DataPort", Version = "1.0.0" };
        private readonly InMemoryLogger<DesignLoader> _inMemoryLogger = new();
        private readonly IServiceProvider _loggingServices;

        public LoggingBehavior()
        {
            _rulesetProvider.AddSystemDataPortFunctionBlock(Arg.Any<CancellationToken>())
                .Returns(_systemDep);

            _loggingServices = new ServiceCollection()
                .AddSingleton<DesignLoader>()
                .AddSingleton<ClusterManagementOptions>()
                .AddSingleton(_fileSystem)
                .AddSingleton(_workspaceService)
                .AddSingleton(_packageStore)
                .AddSingleton(_repository)
                .AddSingleton(_packageContentProvider)
                .AddSingleton(_dependencyStore)
                .AddSingleton(_rulesetProvider)
                .AddTransient<ILogger<DesignLoader>>(_ => _inMemoryLogger)
                .BuildServiceProvider();
        }

        [Fact]
        public async Task Should_log_warning_with_name_and_version_when_package_missing_on_disk()
        {
            // Arrange
            var dep = new ClusterDependency { Name = "ViciOne.Missing", Version = "3.1.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep]);

            _repository.DownloadAndExtract(dep.Name, dep.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/dep", "target/dep"));

            // Package downloaded but not found on disk (no .deps.json files set up)

            using var service = _loggingServices.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert
            _inMemoryLogger.LogEntries.Should().Contain(
                e => e.LogLevel == LogLevel.Warning
                  && e.Message.Contains(dep.Name)
                  && e.Message.Contains(dep.Version));
        }

        [Fact]
        public async Task Should_log_error_with_name_and_version_for_download_failure()
        {
            // Arrange
            var dep = new ClusterDependency { Name = "ViciOne.Broken", Version = "2.5.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep]);

            _repository.DownloadAndExtract(dep.Name, dep.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/broken", "target/broken") { Error = new InvalidOperationException("download failed") });

            using var service = _loggingServices.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert
            _inMemoryLogger.LogEntries.Should().Contain(
                e => e.LogLevel == LogLevel.Error
                  && e.Message.Contains(dep.Name)
                  && e.Message.Contains(dep.Version));
        }

        [Fact]
        public async Task Should_log_warning_for_each_failed_download()
        {
            // Arrange — two download failures
            var depDownloadFail1 = new ClusterDependency { Name = "ViciOne.DownloadFail1", Version = "1.0.0" };
            var depDownloadFail2 = new ClusterDependency { Name = "ViciOne.DownloadFail2", Version = "2.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([depDownloadFail1, depDownloadFail2]);

            _repository.DownloadAndExtract(depDownloadFail1.Name, depDownloadFail1.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/fail1", "target/fail1") { Error = new InvalidOperationException("fail") });

            _repository.DownloadAndExtract(depDownloadFail2.Name, depDownloadFail2.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/fail2", "target/fail2") { Error = new InvalidOperationException("fail") });

            using var service = _loggingServices.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert — a warning with name+version is emitted for each download failure
            _inMemoryLogger.LogEntries.Should().Contain(
                e => e.LogLevel == LogLevel.Warning
                  && e.Message.Contains(depDownloadFail1.Name)
                  && e.Message.Contains(depDownloadFail1.Version));
            _inMemoryLogger.LogEntries.Should().Contain(
                e => e.LogLevel == LogLevel.Warning
                  && e.Message.Contains(depDownloadFail2.Name)
                  && e.Message.Contains(depDownloadFail2.Version));
        }

        [Fact]
        public async Task Should_not_log_download_warning_when_download_succeeds()
        {
            // Arrange
            var dep = new ClusterDependency { Name = "ViciOne.Good", Version = "1.0.0" };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dep]);

            _repository.DownloadAndExtract(dep.Name, dep.Version, Arg.Any<CancellationToken>())
                .Returns(new HttpLoaderResult("source/good", "target/good"));

            SetupPackageOnDisk(dep, "cache/Dependencies");

            _packageContentProvider.TryAddPackage(dep, Arg.Any<string>(), false, Arg.Any<CancellationToken>())
                .Returns(true);

            using var service = _loggingServices.GetRequiredService<DesignLoader>();

            // Act
            await service.StartAsync(CancellationToken.None);
            await service.Task;

            // Assert — no download-failure warning should be emitted
            _inMemoryLogger.LogEntries.Should().NotContain(
                e => e.LogLevel == LogLevel.Warning && e.Message.Contains("Failed to download"));
        }
    }

    /// <summary>
    /// Sets up IFileSystem mocks so that GetComponents extension method returns the given dependency as an existing package on disk.
    /// </summary>
    private void SetupPackageOnDisk(ClusterDependency dependency, string packagesDirectory)
    {
        var depsJsonPath = $"{packagesDirectory}/{dependency.Name}/{dependency.Version}/{dependency.Name}.deps.json";
        var versionDir = $"{packagesDirectory}/{dependency.Name}/{dependency.Version}";
        var nameDir = $"{packagesDirectory}/{dependency.Name}";

        _fileSystem.Directory.GetFiles(Arg.Any<string>(), "*.deps.json", SearchOption.AllDirectories)
            .Returns([depsJsonPath]);
        _fileSystem.Path.GetDirectoryName(depsJsonPath).Returns(versionDir);
        _fileSystem.Path.GetFileName(versionDir).Returns(dependency.Version);
        _fileSystem.Path.GetDirectoryName(versionDir).Returns(nameDir);
        _fileSystem.Path.GetFileName(nameDir).Returns(dependency.Name);
        _fileSystem.Path.IsPathRooted(versionDir).Returns(true);
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

