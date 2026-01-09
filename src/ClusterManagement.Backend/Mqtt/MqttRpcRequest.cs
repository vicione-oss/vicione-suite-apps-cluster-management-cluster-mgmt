using System.Buffers;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace ClusterManagement.Backend.Mqtt;

public sealed record MqttRpcRequest(string Method)
{
    public ReadOnlySequence<byte>? Payload { get; set; }
    public string? ContentType { get; set; }

    public MqttRpcRequest(string method, ReadOnlySequence<byte> payload, string contentType)
        : this(method)
    {
        Payload = payload;
        ContentType = contentType;
    }

    public static MqttRpcRequest PlainText(string method, string text)
        => new(method, new(Encoding.UTF8.GetBytes(text)), MediaTypeNames.Text.Plain);

    public static MqttRpcRequest GZipJson(string method)
        => new(method, ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.GZip);

    public static MqttRpcRequest GZipJson<T>(string method, T payload, JsonSerializerOptions? options = default)
    {
        using MemoryStream memory = new();
        using (GZipStream gzip = new(memory, CompressionMode.Compress))
            JsonSerializer.Serialize(gzip, payload, options);
        return new(method, new(memory.ToArray()), MediaTypeNames.Application.GZip);
    }

    public static MqttRpcRequest Json(string method)
        => new(method, ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.Json);

    public static MqttRpcRequest Json<T>(string method, T payload, JsonSerializerOptions? options = default)
        => new(method, new(JsonSerializer.SerializeToUtf8Bytes(payload, options)), MediaTypeNames.Application.Json);
}
