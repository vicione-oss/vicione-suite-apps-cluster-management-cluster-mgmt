using System.IO.Abstractions;
using System.Runtime.InteropServices;

namespace ClusterManagement.Backend.EngineHosts;

/// <remarks>
/// As long as no module migrations are possible, this type will execute migration logic for deployed engine hosts.
/// When module migrations are implemented, the logic can be moved and this type can be removed.
/// </remarks>
internal interface IEngineHostPool
{
    Task Migrate(CancellationToken cancellationToken);
}

internal sealed class EngineHostPool(string engineHostDownloadDirectory, string hostsDirectory, IFileSystem fileSystem) : IEngineHostPool
{
    public async Task Migrate(CancellationToken cancellationToken)
    {
        // should be executed when upgrading to cluster management 1.2.0
        if (fileSystem.Directory.Exists(hostsDirectory))
        {
            Lazy<string> executable = new(() => fileSystem.Path.Combine(
                engineHostDownloadDirectory,
                $"0.27.0-{RuntimeInformation.RuntimeIdentifier}",
                string.Concat("ViciOne.ManagedEngine.Host", OperatingSystem.IsWindows() ? ".exe" : string.Empty)), true);

            var workingDirectories = fileSystem.Directory.EnumerateDirectories(hostsDirectory);
            foreach (var workingDirectory in workingDirectories)
            {
                var filePath = fileSystem.Path.Combine(workingDirectory, "executable");
                if (!fileSystem.File.Exists(filePath))
                    await fileSystem.File.WriteAllTextAsync(filePath, executable.Value, cancellationToken);
            }
        }
    }
}
