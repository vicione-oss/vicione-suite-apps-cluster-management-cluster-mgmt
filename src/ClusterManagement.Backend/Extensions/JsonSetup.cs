using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Extensions;

internal static class JsonSetup
{
    internal static JsonSerializerOptions DefaultOptions { get; } = new(JsonSerializerOptions.Default)
    {
        Converters =
        {
            new JsonStringEnumConverter(),
        },
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    internal static JsonSerializerOptions PreserveTypeOptions { get; } = new(DefaultOptions)
    {
        Converters =
        {
            new TypeJsonConverter(),
        },
        ReferenceHandler = ReferenceHandler.Preserve,
    };
}
