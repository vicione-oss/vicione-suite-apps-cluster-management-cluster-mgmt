using System.Text.Encodings.Web;
using System.Text.Json;
using ClusterManagement.Public.DataflowEvents;

namespace ClusterManagement.Client.DataflowEvents.Models;

internal class DataflowEventModel : IDataflowEvent
{
    public const int MaxDisplayLength = 150;

    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public required Guid EventId { get; init; }
    public required string Name { get; set; }
    public string? Unit { get; set; }
    public required string ValueType { get; init; }
    public Guid FunctionBlockId { get; init; }
    public string FunctionBlockName { get; init; } = string.Empty;
    public string ConnectorName { get; init; } = string.Empty;
    public string DataflowName { get; init; } = string.Empty;
    public IReadOnlyList<string> ContainerPath { get; init; } = [];

    /// <summary>
    /// The value as the event broker sent it, as JSON.
    /// </summary>
    public string? Value
    {
        get => _value;
        set
        {
            // The broker passes the same value on for every message of an unchanged payload.
            if (value == _value)
                return;

            _value = value;
            ReadableValue = Readable(value);
        }
    }

    /// <summary>
    /// <see cref="Value"/> with escaped characters written as themselves, such as <c>"°C"</c> for <c>"\u00B0C"</c>; the
    /// value is still JSON. Worked out once per value, not for every render.
    /// </summary>
    public string? ReadableValue { get; private set; }

    /// <summary>
    /// The value an input row shows and writes: what was typed, or for a Boolean input the value its checkbox wrote
    /// last. It stays after a write, so the same value can be written again.
    /// </summary>
    public string? PendingValue { get; set; }

    /// <summary>
    /// The dataflow and the containers the function block sits in, such as <c>Line 3 › Pump 1</c>.
    /// </summary>
    public string Location => string.Join(" › ", [DataflowName, .. ContainerPath]);

    public string? DisplayValue
    {
        get
        {
            if (string.IsNullOrEmpty(ReadableValue))
                return null;

            if (ReadableValue.Length <= MaxDisplayLength)
                return ReadableValue;

            return string.Concat(ReadableValue.AsSpan(0, MaxDisplayLength), "...");
        }
    }
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// <c>false</c> when the engine marked the last value invalid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// The cycle of the engine that sent the last value, when its message names it.
    /// </summary>
    public ulong? EngineCycle { get; set; }

    // The engine host writes JSON with every character outside ASCII escaped. Rewritten without those escapes, it reads
    // as the value; the text is shown as text, so nothing needs escaping for HTML.
    private static readonly JsonSerializerOptions s_readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private string? _value;

    private static string? Readable(string? value)
    {
        if (value is null || !value.Contains("\\u", StringComparison.Ordinal))
            return value;

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, s_readable);
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
