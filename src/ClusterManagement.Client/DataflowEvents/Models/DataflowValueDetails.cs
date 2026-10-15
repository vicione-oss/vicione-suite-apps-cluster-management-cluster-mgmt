namespace ClusterManagement.Client.DataflowEvents.Models;

/// <summary>
/// What the engine host sent along with a value of a dataflow event.
/// </summary>
/// <param name="IsValid"><c>false</c> when the engine marked the value invalid.</param>
/// <param name="EngineCycle">The cycle of the engine that sent the value, when the message names it.</param>
public readonly record struct DataflowValueDetails(bool IsValid, ulong? EngineCycle);
