using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Reflection;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.TestUtils;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Backend.Modules;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.SystemConfiguration;
using Sdk.Testing;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using ViciOne.Core.Dataflow.DataModel.Generation;
using ViciOne.Core.Dataflow.Pooling;
using Constants = ClusterManagement.Public.Constants;

namespace ClusterManagement.Backend.Tests;

/// <summary>
/// Starts the ViciOne Suite with settings of the regular configuration using environment "Development".
/// </summary>
internal sealed partial class ClusterSystemTestContext : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly TemporaryDirectory _engineHostsDirectory;
    private readonly TemporaryDirectory _workspaceDirectory;
    private readonly FileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IClusterSnapshotStore _snapshotStore;
    private readonly InMemoryLoggerProvider _inMemoryLogging = new();
    private readonly ISuiteMediator _suiteMediator = Substitute.For<ISuiteMediator>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Guid _correlationId = Guid.NewGuid();

    private static readonly MqttConnection s_mqttConnection = new()
    {
        Address = "localhost",
        Port = 1883,
    };
    private readonly List<object> _events = [];

    internal IServiceProvider Services => _services;
    internal Guid ClusterId => _cluster.Id;
    internal Guid CorrelationId => _correlationId;
    internal Version ClusterVersion => _cluster.Version;
    internal CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public IReadOnlyCollection<object> Events => _events;

    private ClusterSystemTestContext(InstanceType instanceType)
    {
        ServiceCollection services = new();

        // suite provided services and logic
        _workspaceDirectory = new();

        _workspaceProvider.Home.Returns(_workspaceDirectory.Path);
        _workspaceProvider.Cache.Returns(_workspaceDirectory.Path);
        services.AddSingleton(s => _workspaceProvider);
        CopyJsonFiles();
        CopyFunctionBlockDesigns(_workspaceDirectory);

        services.AddSingleton(CreateInstanceInformationProvider(instanceType));
        services.AddScoped(p => Substitute.For<IControlServiceManagement>());

        var moduleId = ModuleIdResolver.ResolveId<ClusterManagementBackendModule>();
        var configuration = new TestConfig().AddModuleWithOptions(moduleId, ClusterManagementTools.ClusterManagementOptions);
        services.AddConfiguration(configuration);

        ClusterManagementBackendModule module = new();
        module.ConfigureServices(services, configuration.BuildConfiguration(), Substitute.For<IMvcBuilder>());

        services.AddSingleton<IFileSystem>(_fileSystem);

        services.AddLogging(builder => builder.AddProvider(_inMemoryLogging));

        _suiteMediator.When(x => x.Publish(Arg.Any<Sdk.Messaging.IEvent>(), Arg.Is(_cancellationTokenSource.Token))).Do(x => AddEvent(x[0]));
        services.AddScoped(s => _suiteMediator);

        // cluster management services and logic
        SetupRequestGetConnections(_suiteMediator);

        services.AddSingleton(BuildDesignStore());

        foreach (var hostedService in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList())
            services.Remove(hostedService);

        _services = services.BuildServiceProvider(validateScopes: true);

        _snapshotStore = _services.GetRequiredService<IClusterSnapshotStore>();
        _engineHostsDirectory = new(_fileSystem.GetDeploymentsPath(_workspaceProvider));
    }

    private static IDesignProvider BuildDesignStore()
    {
        var designStore = Substitute.For<IDesignProvider>();
        designStore.CreateResolver().Returns(BuildClusterDependencyResolver());
        return designStore;
    }

    private static Task<IDependencyResolver> BuildClusterDependencyResolver()
    {
        var functionBlockType = typeof(DummyFunctionBlock);
        var dummyFunctionBlockAssemblyName = functionBlockType.Assembly.GetName();
        PoolingFactory poolingFactory = new([]);
        var designInfo = RuntimeFunctionBlockAssemblyAnalyzer.GetFunctionBlockDesign(functionBlockType);
        var dummyFunctionBlockDesign = FunctionBlockDesignGenerator.GenerateFunctionBlockDesign(designInfo, poolingFactory);
        return Task.FromResult<IDependencyResolver>(new ClusterDependencyResolver(
        [
            new (new ViciOne.Cluster.Model.ClusterDependency()
            {
                Name = dummyFunctionBlockAssemblyName.Name!,
                Version = dummyFunctionBlockAssemblyName.Version!,
            }, [dummyFunctionBlockDesign,], []),
        ]));
    }

    internal static async Task<ClusterSystemTestContext> Create(InstanceType instanceType = InstanceType.Standalone)
    {
        ClusterSystemTestContext context = new(instanceType);
        await context.LoadCluster();
        context.SetupRequestGetClusterInfosRequest();
        return context;
    }

    private void CopyJsonFiles()
    {
        var sourcePath = _fileSystem.Path.Combine(GetRootPath(), "src", typeof(ClusterManagementBackendModule).Namespace!, ClusterDependencyStore.PackagesRessourceFolder);
        var sourceFile = ClusterDependencyStore.GetPackagesFilepath(sourcePath, default, _fileSystem);
        var targetFile = ClusterDependencyStore.GetPackagesFilepath(_workspaceProvider.Home, default, _fileSystem);
        _fileSystem.File.Copy(sourceFile, targetFile);
    }

    private void CopyFunctionBlockDesigns(TemporaryDirectory dir)
    {
        var clusterManagementTestsAssembly = Assembly.GetExecutingAssembly();
        var clusterManagementTestsAssemblyLocation = clusterManagementTestsAssembly.Location;
        var clusterManagementTestsAssemblyName = clusterManagementTestsAssembly.GetName();

        var dummyFunctionBlockAssemblyName = typeof(DummyFunctionBlock).Assembly.GetName();
        var clusterManagementDummyFunctionBlockAssemblyLocation = clusterManagementTestsAssemblyLocation
            .Replace(clusterManagementTestsAssemblyName.Name!, dummyFunctionBlockAssemblyName.Name!, StringComparison.OrdinalIgnoreCase)
            .Replace("ClusterManagement.Tests", typeof(DummyFunctionBlock).FullName, StringComparison.OrdinalIgnoreCase);
        var clusterManagementDummyFunctionBlockDepsLocation = _fileSystem.Path.ChangeExtension(clusterManagementDummyFunctionBlockAssemblyLocation, "deps.json");

        var packageDirectory = dir.CreateDirectory(Backend.Designs.Extensions.IFileSystemExtensions.DependenciesDirectory, dummyFunctionBlockAssemblyName.Name!, dummyFunctionBlockAssemblyName.Version!.ToString());
        _fileSystem.File.Copy(clusterManagementDummyFunctionBlockAssemblyLocation, _fileSystem.Path.Combine(packageDirectory, _fileSystem.Path.GetFileName(clusterManagementDummyFunctionBlockAssemblyLocation)));
        _fileSystem.File.Copy(clusterManagementDummyFunctionBlockDepsLocation, _fileSystem.Path.Combine(packageDirectory, _fileSystem.Path.GetFileName(clusterManagementDummyFunctionBlockDepsLocation)));
    }

    private static void SetupRequestGetConnections(ISuiteMediator mediator)
    {
        Connection connection = new()
        {
            Name = "TestMqtt",
            Tags = [Constants.CentralBroker]
        };

        connection.SetMqttConnection(s_mqttConnection);
        GetConnectionsResponse response = new([connection]);

        mediator.Request<GetConnections, GetConnectionsResponse>(default!, default).ReturnsForAnyArgs(response);
    }

    private void SetupRequestGetClusterInfosRequest()
    {
        GetClusterInfosResponse response = new()
        {
            Results =
            [
                new(_clusterBuilder.Cluster.Id, _clusterBuilder.Cluster.Version, string.Empty, true),
            ],
        };
        _suiteMediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(default!, default).ReturnsForAnyArgs(response);
    }

    private static IInstanceInformationProvider CreateInstanceInformationProvider(InstanceType instanceType)
    {
        var instanceInformation = Substitute.For<IInstanceInformation>();
        instanceInformation.Name.Returns("Test");
        instanceInformation.Id.Returns(Guid.NewGuid());
        instanceInformation.Type.Returns(instanceType);

        var instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
        instanceInformationProvider.Local.Returns(instanceInformation);

        return instanceInformationProvider;
    }

    public async ValueTask DisposeAsync()
    {
        _clusterBuilder.Dispose();
        _cancellationTokenSource.Dispose();

        KillEngineHosts(_engineHostsDirectory.Path);
        SqliteConnection.ClearAllPools();

        _engineHostsDirectory.Dispose();
        _workspaceDirectory.Dispose();
        _inMemoryLogging.Dispose();
        await _services.DisposeAsync();
    }

    private static void KillEngineHosts(string directory)
    {
        var processIdFiles = Directory.GetFiles(directory, "processid", SearchOption.AllDirectories);
        foreach (var processIdFile in processIdFiles)
        {
            var process = GetEngineHostProcess(processIdFile);
            process.Kill(true);
        }
    }

    private static Process GetEngineHostProcess(string processIdFile)
    {
        var content = File.ReadAllText(processIdFile);
        var id = int.Parse(content, CultureInfo.InvariantCulture);
        var process = Process.GetProcessById(id);
        return process;
    }

    internal void AddEvent(object e) => _events.Add(e);

    internal async Task When(Func<ClusterSystemTestContext, Task> action)
    {
        _snapshotStore.Delete(_clusterBuilder.Cluster.Id, _clusterBuilder.Cluster.Version);
        await _snapshotStore.Store(_clusterBuilder.Cluster, _cancellationTokenSource.Token);
        await action(this);
    }

    internal async Task<T> When<T>(Func<ClusterSystemTestContext, Task<T>> action)
    {
        _snapshotStore.Delete(_clusterBuilder.Cluster.Id, _clusterBuilder.Cluster.Version);
        await _snapshotStore.Store(_clusterBuilder.Cluster, _cancellationTokenSource.Token);
        return await action(this);
    }

    internal void Then(Action<ClusterSystemTestContext> assertion)
    {
        using AssertionScope scope = new();
        scope.AppendTracing(_inMemoryLogging.LastMessage);
        assertion(this);
    }

    internal void Then<T>(Action<ClusterSystemTestContext, T> assertion, T arg)
    {
        using AssertionScope scope = new();
        scope.AppendTracing(_inMemoryLogging.LastMessage);
        assertion(this, arg);
    }

    internal void Then_no_errors_were_logged()
        => _inMemoryLogging.LogEntries.Should().NotContain(static e => e.LogLevel == LogLevel.Error || e.LogLevel == LogLevel.Critical);

    private static string GetRootPath() => GetPathContains(
    [
        "README.md",
        "CHANGELOG.md"
    ]);

    private static string GetPathContains(IList<string> fileNames)
    {
        return GetDeeper(new DirectoryInfo(Environment.CurrentDirectory));

        string GetDeeper(DirectoryInfo directory)
        {
            // if we find these files we assume we are in repos root
            foreach (var fileName in fileNames)
            {
                if (!File.Exists(Path.Combine(directory.FullName, fileName)))
                    break;

                return directory.FullName;
            }

            return directory.Parent is null ? string.Empty : GetDeeper(directory.Parent);
        }
    }
}
