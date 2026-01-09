using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal static partial class SeriLogReader
{
    private static readonly Regex s_header = Header();

    internal static async Task<IReadOnlyCollection<LogEntry>> ReadFile(IFileSystem fileSystem, string filePath, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.FileStream.New(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        List<LogEntry> entries = [];
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                continue;

            var match = s_header.Match(line);
            if (!match.Success)
                continue;

            var text = match.Groups[3].Value;
            LogEntry entry = new()
            {
                Timestamp = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss.FFF zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                Type = match.Groups[2].Value.ToUpperInvariant() switch
                {
                    "V" or "VB" or "VRB" or "VERB" or "VERBO" or "VERBOS" or "VERBOSE" => LogLevel.Trace,
                    "D" or "DE" or "DBG" or "DBUG" or "DEBUG" => LogLevel.Debug,
                    "I" or "IN" or "INF" or "INFO" or "INFOR" or "INFORM" or "INFORMA" or "INFORMAT" or "INFORMATI" or "INFORMATIO" or "INFORMATION" => LogLevel.Information,
                    "W" or "WN" or "WRN" or "WARN" or "WARNI" or "WARNIN" or "WARNING" => LogLevel.Warning,
                    "E" or "ER" or "ERR" or "EROR" or "ERROR" => LogLevel.Error,
                    "F" or "FA" or "FTL" or "FATL" or "FATAL" => LogLevel.Critical,
                    _ => LogLevel.None,
                },
                Text = text,
            };
            entries.Add(entry);
        }
        return entries;
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?\s[+-]\d{2}:\d{2})\s\[(\w{3})\]\s?(.*)$")]
    private static partial Regex Header();
}
