using System.Globalization;
using System.Text;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ClusterManagement.Client.Management.Extensions;

/// <summary>
/// partial copy of MQTTnet.Extensions.MqttApplicationMessageExtensions
/// </summary>
public static class MqttApplicationMessageExtensions
{
    public static MqttUserProperty FindRequiredUserProperty(this MqttApplicationMessage message, string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => message.UserProperties.FindOptional(name, comparison) ?? throw new InvalidOperationException("MQTT user property '" + name + "' could not be found.");

    public static MqttUserProperty FindRequired(this IEnumerable<MqttUserProperty> userProperties, string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        => userProperties.FindOptional(name, comparison) ?? throw new InvalidOperationException("MQTT user property '" + name + "' could not be found.");

    public static MqttUserProperty? FindOptional(this IEnumerable<MqttUserProperty> userProperties, string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var name2 = name;
        return userProperties.FirstOrDefault(_ => _.Name.Equals(name2, comparison));
    }
    public static MqttApplicationMessageBuilder WithUserProperty(this MqttApplicationMessageBuilder builder, string name, DateTime dateTime)
        => builder.WithUserProperty(name, Encoding.UTF8.GetBytes(dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    public static MqttApplicationMessageBuilder WithJsonPayload(this MqttApplicationMessageBuilder builder, byte[] json)
        => builder.WithPayload(json).WithPayloadFormatIndicator(MqttPayloadFormatIndicator.CharacterData).WithContentType("application/json");

    public static DateTime GetDateTime(this MqttUserProperty userProperty)
        => DateTime.ParseExact(userProperty.ReadValueAsString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}
