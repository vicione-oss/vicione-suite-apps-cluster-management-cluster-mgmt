using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Engines;

public sealed record TypedObject(string Type, [StringSyntax(StringSyntaxAttribute.Json)] string? Value = default)
{
    public string Type { get; set; } = Type;
    [StringSyntax(StringSyntaxAttribute.Json)]
    public string? Value { get; set; } = Value;

    internal static TypedObject Empty { get; } = CreateMandatory(new());

    public static TypedObject? CreateOptional(object? instance = default) => instance is null ? default : CreateMandatory(instance);

    public static TypedObject CreateMandatory<T>(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance, nameof(instance));
        return CreateMandatory(typeof(T), instance);
    }

    public static TypedObject CreateMandatory(object instance) => CreateMandatory(instance.GetType(), instance);

    private static TypedObject CreateMandatory(Type type, object instance)
    {
        TypeNameHandlingWrapper wrapper = new() { Instance = instance, };
        var jsonObject = (JsonObject)JsonSerializer.SerializeToNode(wrapper, JsonSetup.PreserveTypeOptions)!;
        var instanceJson = jsonObject[nameof(TypeNameHandlingWrapper.Instance)]!;
        var typeName = type.AssemblyQualifiedName ?? throw new InvalidOperationException("Could not find 'Type' property in JSON. Maybe managed engine changed type name handling serialization.");
        var valueJson = instanceJson[nameof(Value)]?.ToString();
        return new(typeName, valueJson);
    }

    public JsonNode ToJsonNode()
        => new JsonObject()
        {
            [nameof(Type)] = JsonValue.Create<string>(Type),
            [nameof(Value)] = JsonValue.Create<string>(Value),
        };

    private sealed class TypeNameHandlingWrapper
    {
        [JsonConverter(typeof(TypeNameHandlingConverter))]
        public object Instance { get; set; } = default!;
    }
}
