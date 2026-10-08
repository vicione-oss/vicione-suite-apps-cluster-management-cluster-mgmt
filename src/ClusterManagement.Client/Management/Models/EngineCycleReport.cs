using System.Buffers;
using System.Text.Json;
using ClusterManagement.Public;

namespace ClusterManagement.Client.Management.Models;

/// <summary>
/// One cycle of an engine host as published on <c>{engineHostId}/cycle/info</c>; durations in milliseconds.
/// </summary>
/// <remarks>
/// Engine hosts publish a report on every cycle, so parsing reads the payload directly instead of through the serializer.
/// </remarks>
public sealed record EngineCycleReport(double CycleDuration, IReadOnlyList<EngineCycleChainLink> Deployments)
{
    private const string InfoTopicPath = $"{Constants.EngineMonitoringTopicPostFix}/info";

    public static string InfoTopic(Guid engineHostId)
        => $"{engineHostId}/{InfoTopicPath}";

    // Checks the topic in place, since every report passes through here.
    public static bool TryParseEngineHostId(string topic, out Guid engineHostId)
    {
        var separator = topic.IndexOf('/', StringComparison.Ordinal);
        engineHostId = Guid.Empty;

        return separator > 0
            && topic.AsSpan(separator + 1).SequenceEqual(InfoTopicPath)
            && Guid.TryParse(topic.AsSpan(0, separator), out engineHostId);
    }

    /// <summary>
    /// <c>null</c> for payloads that are not a cycle report. Chain links without a deployment id are left out.
    /// </summary>
    public static EngineCycleReport? TryParse(ReadOnlySequence<byte> payload)
    {
        try
        {
            var reader = new Utf8JsonReader(payload);
            return reader.Read() && reader.TokenType == JsonTokenType.StartObject ? ReadReport(ref reader) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static EngineCycleReport? ReadReport(ref Utf8JsonReader reader)
    {
        double? cycleDuration = null;
        List<EngineCycleChainLink>? deployments = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (IsProperty(ref reader, "CycleDuration"u8, "cycleDuration"u8))
            {
                if (ReadNumber(ref reader) is not { } value)
                    return null;

                cycleDuration = value;
            }
            else if (IsProperty(ref reader, "Deployments"u8, "deployments"u8))
            {
                reader.Read();
                deployments = reader.TokenType == JsonTokenType.StartArray ? ReadChainLinks(ref reader) : null;
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        return cycleDuration is { } duration && deployments is not null ? new EngineCycleReport(duration, deployments) : null;
    }

    private static List<EngineCycleChainLink> ReadChainLinks(ref Utf8JsonReader reader)
    {
        var links = new List<EngineCycleChainLink>();
        while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            Guid? deployment = null;
            double? duration = null;

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (IsProperty(ref reader, "Deployment"u8, "deployment"u8))
                {
                    reader.Read();
                    deployment = reader.TokenType == JsonTokenType.String && reader.TryGetGuid(out var parsed) ? parsed : null;
                }
                else if (IsProperty(ref reader, "Duration"u8, "duration"u8))
                {
                    duration = ReadNumber(ref reader);
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            if (deployment is { } deploymentId && duration is { } milliseconds)
                links.Add(new EngineCycleChainLink(deploymentId, milliseconds));
        }

        return links;
    }

    // A value that is not a number, such as null, a string or one out of range, is skipped and read as null.
    private static double? ReadNumber(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value))
            return value;

        reader.Skip();
        return null;
    }

    private static bool IsProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> pascalCase, ReadOnlySpan<byte> camelCase)
        => reader.ValueTextEquals(pascalCase) || reader.ValueTextEquals(camelCase);
}

/// <summary>
/// The duration of one engine in a cycle report, under the engine's deployment id.
/// </summary>
public sealed record EngineCycleChainLink(Guid Deployment, double Duration);
