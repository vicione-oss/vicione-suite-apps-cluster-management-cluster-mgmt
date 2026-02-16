using System.Reflection;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ClusterManagement.Backend.Tests.TestUtils;

public class JsonEqualityComparer<T> : EqualityComparer<T>
{
    private readonly JsonSerializerSettings _settings;

    public JsonEqualityComparer()
        : this([])
    {
    }

    public JsonEqualityComparer(params (Type, string)[] membersToIgnore)
    {
        _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ContractResolver = new AllPropertiesContractResolver(membersToIgnore),
        };
        _settings.Converters.Add(new JsonNodeConverter());
    }

    public override bool Equals(T? x, T? y)
    {
        Assert.Equal(JsonConvert.SerializeObject(x, _settings), JsonConvert.SerializeObject(y, _settings));
        return true;
    }

    public override int GetHashCode(T obj)
        => throw new NotImplementedException();

    private class AllPropertiesContractResolver : DefaultContractResolver
    {
        private readonly (Type DeclaringType, string PropertyName)[] _membersToIgnore;

        internal AllPropertiesContractResolver(params (Type, string)[] membersToIgnore) => _membersToIgnore = membersToIgnore;

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            var members = objectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.CanRead)
                .Where(p => !p.DeclaringType?.Namespace?.StartsWith("System.Text.Json", StringComparison.OrdinalIgnoreCase) ?? true)
                .Cast<MemberInfo>();

            if (_membersToIgnore.Length > 0)
                members = members.Where(m => !_membersToIgnore.Any(i => i.DeclaringType == m.DeclaringType && i.PropertyName == m.Name));

            return [.. members];
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);

            foreach (var property in properties)
                property.Readable = true;

            return properties;
        }
    }

    private sealed class JsonNodeConverter : JsonConverter<JsonNode>
    {
        public override JsonNode? ReadJson(JsonReader reader, Type objectType, JsonNode? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var json = reader.ReadAsString() ?? string.Empty;
            return JsonNode.Parse(json);
        }

        public override void WriteJson(JsonWriter writer, JsonNode? value, JsonSerializer serializer)
        {
            if (value is not null)
            {
                var json = value.ToJsonString();
                writer.WriteRawValue(json);
            }
        }
    }
}
