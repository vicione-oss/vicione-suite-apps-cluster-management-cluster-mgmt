using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ClusterManagement.Public.Extensions;

public static class ClusterManagementFormatExtensions
{
    [return: NotNullIfNotNull(nameof(value))]
    public static string? ToClusterManagementFormat(this object? value)
        => value is null ? null : JsonSerializer.Serialize(value);
}
