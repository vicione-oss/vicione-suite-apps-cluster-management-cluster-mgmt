using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Backend.EngineHosts;

internal static class EngineHostAppSettingsFactory
{
    internal static EngineHostAppSettings Create(Guid id, string name, MqttConnection brokerConnection, LogLevel logLevel, string packagesDirectory)
    {
        var serilogSink = SerilogSink.Console();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            serilogSink = SerilogSink.Journal(id, name, "[{SourceContext}] {Message:lj}{NewLine}{Exception}");

        return new()
        {
            Id = id,
            CommandBus = new()
            {
                Type = brokerConnection.Protocol.ToString(),
                Host = brokerConnection.Address,
                Port = brokerConnection.Port,
                Username = brokerConnection.Username,
                Password = brokerConnection.Password,
                MaxPendingMessages = 10_000,
            },
            DeploymentsDirectory = "deployments",
            PackagesDirectory = packagesDirectory,
            Serilog = new()
            {
                MinimumLevel = new(TranslateLogLevel(logLevel)),
                WriteTo = [serilogSink,],
            },
        };
    }

    private static string TranslateLogLevel(LogLevel logLevel)
        => logLevel switch
        {
            LogLevel.None or LogLevel.Critical => "Fatal",
            LogLevel.Error => "Error",
            LogLevel.Warning => "Warning",
            LogLevel.Information => "Information",
            LogLevel.Debug => "Debug",
            _ => "Verbose",
        };
}
