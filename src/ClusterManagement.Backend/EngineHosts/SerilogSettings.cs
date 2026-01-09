using System.Text.Json.Serialization;
using Sdk.Modules;

namespace ClusterManagement.Backend.EngineHosts;

public sealed record SerilogSettings
{
    public SerilogMinimumLevel MinimumLevel { get; set; } = new("Warning");
    public List<SerilogSink> WriteTo { get; init; } = [];
}

public sealed record SerilogMinimumLevel(string Default);

public sealed record SerilogSink
{
    public required string Name { get; init; }
    public object? Args { get; init; }

    private SerilogSink() { }

    public static SerilogSink File(string path, string? outputTemplate = null) => new()
    {
        Name = "File",
        Args = new SerilogFileSinkArguments
        {
            Path = path,
            OutputTemplate = outputTemplate,
        },
    };

    public static SerilogSink Console(string? outputTemplate = null) => new()
    {
        Name = "Console",
        Args = new SerilogConsoleSinkArguments
        {
            OutputTemplate = outputTemplate,
        },
    };

    public static SerilogSink LocalSyslog(string appName, string? outputTemplate = null) => new()
    {
        Name = "LocalSyslog",
        Args = new SerilogLocalSyslogSinkArguments
        {
            AppName = appName,
            OutputTemplate = outputTemplate,
        },
    };

    public static SerilogSink Journal(Guid engineHostId, string engineHostName, string? outputTemplate = null) => new()
    {
        Name = "Journal",
        Args = new SerilogJournalArguments
        {
            OutputTemplate = outputTemplate,
            MetaDataFields = new Dictionary<string, object?>()
            {
                {"ENGINE_HOST_ID", engineHostId.ToString() },
                {"ENGINE_HOST_NAME", engineHostName },
                {"MODULE_ID", ModuleIdResolver.ResolveId<ClusterManagementBackendModule>() },
            },
        },
    };

    public static string FormatAppName(Guid id, string name) => $"{name} ({id})";
}

internal sealed record SerilogFileSinkArguments
{
    public string Path { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputTemplate { get; set; }
}

internal sealed record SerilogConsoleSinkArguments
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputTemplate { get; set; }
}

internal sealed record SerilogLocalSyslogSinkArguments
{
    public string AppName { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputTemplate { get; set; }
}

internal sealed record SerilogJournalArguments
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputTemplate { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? MetaDataFields { get; set; }
}
