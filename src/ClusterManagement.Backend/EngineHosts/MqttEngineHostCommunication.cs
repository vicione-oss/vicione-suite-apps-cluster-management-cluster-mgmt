using ClusterManagement.Backend.EngineHosts.CommunicationContracts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Migrations;
using ClusterManagement.Backend.Mqtt;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

internal sealed class MqttEngineHostCommunication : IEngineHostCommunication
{
    private readonly IMqttRpcClient _mqttClient;
    private readonly IEngineHostPoolCommunication _poolCommunication;
    private readonly IReadOnlyCollection<IMigration<IDeployRequest>> _deployMigrations;
    private readonly IReadOnlyCollection<IMigration<IStartParameter>> _startParameterMigrations;

    private static IReadOnlyCollection<IMigration<IDeployRequest>> SupportedDeployMigrations { get; } = [];
    private static IReadOnlyCollection<IMigration<IStartParameter>> SupportedStartParameterMigrations { get; } = [];

    public MqttEngineHostCommunication(IMqttRpcClient mqttClient, IEngineHostPoolCommunication poolCommunication)
        : this(mqttClient, poolCommunication, SupportedDeployMigrations, SupportedStartParameterMigrations) { }

    internal MqttEngineHostCommunication(IMqttRpcClient mqttClient, IEngineHostPoolCommunication poolCommunication,
        IReadOnlyCollection<IMigration<IDeployRequest>> deployMigrations,
        IReadOnlyCollection<IMigration<IStartParameter>> startParameterMigrations)
    {
        _mqttClient = mqttClient;
        _poolCommunication = poolCommunication;
        _deployMigrations = deployMigrations;
        _startParameterMigrations = startParameterMigrations;
    }

    public async Task<Guid> DeployEngine(Guid engineHostId, Workflow workflow, CancellationToken cancellationToken)
    {
        var managedEngineStartParameter = await CreateManagedEngineStartParameter(engineHostId, workflow, cancellationToken);
        var engineHostDeployRequest = await CreateEngineHostDeploy(engineHostId, workflow, managedEngineStartParameter, cancellationToken);
        var mqttRequest = MqttRpcRequest.GZipJson<object>("deploy", engineHostDeployRequest, JsonSetup.DefaultOptions);
        var response = await _mqttClient.RequestFromWorker(mqttRequest, engineHostId, cancellationToken);
        return response.GZipJson<Guid>();
    }

    private async Task<IStartParameter> CreateManagedEngineStartParameter(Guid engineHostId, Workflow workflow, CancellationToken cancellationToken)
    {
        IStartParameter managedEngineStartParameter = workflow.ToStartParameter();
        var managedEngineVersion = await GetManagedEngineVersion(engineHostId, cancellationToken);
        managedEngineStartParameter = _startParameterMigrations.Execute(managedEngineStartParameter, managedEngineVersion);
        return managedEngineStartParameter;
    }

    private async Task<SemVersion> GetManagedEngineVersion(Guid engineHostId, CancellationToken cancellationToken)
    {
        const string message = "Could not get version of managed engine.";
        var version = await _poolCommunication.GetManagedEngineVersion(cancellationToken, engineHostId)
            .CatchAs(static exception => new InvalidOperationException(message, exception), cancellationToken);
        return version ?? throw new InvalidOperationException(message);
    }

    private async Task<IDeployRequest> CreateEngineHostDeploy(Guid engineHostId, Workflow workflow, IStartParameter startParameter,
        CancellationToken cancellationToken)
    {
        IDeployRequest deployRequest = new DeployRequest()
        {
            CycleTime = workflow.Engine.CycleTime,
            EngineChainIndex = workflow.Engine.ChainIndex,
            PackageReferences = workflow.ToPackageReferences(),
            StartParameter = startParameter,
        };
        var engineHostVersion = await GetEngineHostVersion(engineHostId, cancellationToken);
        deployRequest = _deployMigrations.Execute(deployRequest, engineHostVersion);
        return deployRequest;
    }

    private async Task<SemVersion> GetEngineHostVersion(Guid engineHostId, CancellationToken cancellationToken)
    {
        const string message = "Could not get version of engine host.";
        var version = await _poolCommunication.GetEngineHostVersion(cancellationToken, engineHostId)
            .CatchAs(static exception => new InvalidOperationException(message, exception), cancellationToken);
        return version ?? throw new InvalidOperationException(message);
    }

    public async Task<EngineState> GetEngineState(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken)
    {
        var request = MqttRpcRequest.Json("getstate", deploymentIdentifier);
        var response = await _mqttClient.RequestFromWorker(request, engineHostId, cancellationToken);
        var state = response.Json<Engines.CommunicationContracts.EngineState>(JsonSetup.DefaultOptions);
        return TranslateState(state);
    }

    public async Task<IReadOnlyDictionary<Guid, EngineState>> GetDeploymentStates(Guid engineHostId, CancellationToken cancellationToken)
    {
        var request = MqttRpcRequest.GZipJson("getdeploymentstates", new object());
        var response = await _mqttClient.RequestFromWorker(request, engineHostId, cancellationToken);
        var states = response.GZipJson<IReadOnlyDictionary<Guid, Engines.CommunicationContracts.EngineState>>(JsonSetup.DefaultOptions);
        return states.ToDictionary(static state => state.Key, static state => TranslateState(state.Value));
    }

    private static EngineState TranslateState(Engines.CommunicationContracts.EngineState state)
        => state switch
        {
            Engines.CommunicationContracts.EngineState.Stopped => EngineState.Stopped,
            Engines.CommunicationContracts.EngineState.Starting => EngineState.Starting,
            Engines.CommunicationContracts.EngineState.StartFailed => EngineState.StartFailed,
            Engines.CommunicationContracts.EngineState.Running => EngineState.Running,
            Engines.CommunicationContracts.EngineState.Stopping => EngineState.Stopping,
            _ => throw new NotSupportedException($"Engine state '{state}' is not supported."),
        };

    public async Task<bool> StartEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken)
    {
        var request = MqttRpcRequest.Json("start", deploymentIdentifier);
        var response = await _mqttClient.RequestFromWorker(request, engineHostId, cancellationToken);
        return response.Json<bool>();
    }

    public async Task<bool> StopEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken)
    {
        var request = MqttRpcRequest.Json("stop", deploymentIdentifier);
        var response = await _mqttClient.RequestFromWorker(request, engineHostId, cancellationToken);
        return response.Json<bool>();
    }

    public Task TearDownEngine(Guid engineHostId, Guid deploymentIdentifier, CancellationToken cancellationToken)
    {
        var request = MqttRpcRequest.Json("teardown", deploymentIdentifier);
        return _mqttClient.RequestFromWorker(request, engineHostId, cancellationToken);
    }
}

