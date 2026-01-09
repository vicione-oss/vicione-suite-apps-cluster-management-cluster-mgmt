using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Extensions;

internal sealed class PreserveTypeJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize(ref reader, typeToConvert, JsonSetup.PreserveTypeOptions);

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, JsonSetup.PreserveTypeOptions);
}
