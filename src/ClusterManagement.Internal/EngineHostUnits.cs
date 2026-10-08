namespace ClusterManagement.Internal;

/// <summary>
/// The systemd units engine hosts run in when they run as Linux services.
/// </summary>
public static class EngineHostUnits
{
    private const string PrivilegeIndicator = "-privileged";

    public static string Of(Guid engineHostId, bool elevatedPrivileges)
        => elevatedPrivileges ? $"enginehost{PrivilegeIndicator}@{engineHostId}.service" : $"enginehost@{engineHostId}.service";

    /// <summary>
    /// Matches the unit of the engine host with and without elevated privileges, as <c>systemctl list-units</c> takes it.
    /// </summary>
    public static string Pattern(Guid engineHostId)
        => $"enginehost*@{engineHostId}.service";

    /// <summary>
    /// The reset unit of the engine host, which the backend starts when it cannot write the host's configuration.
    /// </summary>
    public static string Reset(Guid engineHostId)
        => $"enginehost-reset@{engineHostId}.service";

    public static bool IsPrivileged(string unit)
        => unit.Contains(PrivilegeIndicator, StringComparison.OrdinalIgnoreCase);
}
