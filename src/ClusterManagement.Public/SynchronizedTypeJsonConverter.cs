using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClusterManagement.Public;

/// <summary>
/// Lets several clusters be read at the same time through the shared serializer options: the managed engine's
/// <see cref="TypeJsonConverter"/> caches resolved types in a dictionary that is not safe for concurrent use.
/// </summary>
internal sealed class SynchronizedTypeJsonConverter(JsonConverter<Type> inner) : JsonConverter<Type>
{
    private readonly Lock _lock = new();

    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        lock (_lock)
            return inner.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
        => inner.Write(writer, value, options);
}
