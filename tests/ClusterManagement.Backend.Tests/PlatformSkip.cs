namespace ClusterManagement.Backend.Tests;

internal static class PlatformSkip
{
    public static bool IfNotLinux => !OperatingSystem.IsLinux();

    public static bool IfNotWindows => !OperatingSystem.IsWindows();

    public static bool IfNotMacOS => !OperatingSystem.IsMacOS();
}
