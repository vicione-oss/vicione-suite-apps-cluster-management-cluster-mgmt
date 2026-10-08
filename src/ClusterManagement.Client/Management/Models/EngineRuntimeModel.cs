using ClusterManagement.Internal.Requests;

namespace ClusterManagement.Client.Management.Models;

/// <summary>
/// An engine as the runtime panel shows it; <c>MinCycleTime</c> is its configured cycle time in milliseconds.
/// </summary>
public sealed record EngineRuntimeModel(Guid InstanceId, Guid EngineId, string EngineName, uint MinCycleTime)
{
    /// <summary>
    /// <c>false</c> until the engine's instance answered or the request for its state failed.
    /// </summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// <c>null</c> while unknown or when the engine host did not answer.
    /// </summary>
    public EngineState? State { get; init; }

    public bool IsReachable { get; init; }

    /// <summary>
    /// <c>false</c> when the instance has no deployment of the engine, for example while a deployment is in progress
    /// or after a failed one.
    /// </summary>
    public bool IsDeployed { get; init; } = true;

    /// <summary>
    /// Id the engine host reports the engine's cycle durations under; known once the state was queried.
    /// </summary>
    public Guid? DeploymentId { get; init; }

    public bool IsRunning => State == EngineState.Running;
}
