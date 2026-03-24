using System.Diagnostics;

namespace ClusterManagement.Internal.Contracts;

[DebuggerDisplay("Added:{Added} Merged:{Merged} Skipped:{Skipped} Errors:{Errors.Count}")]
public class ClusterImportResultItem
{
    public int Added { get; set; }
    public int Skipped { get; set; }
    public int Merged { get; set; }
    public List<string> Errors { get; } = [];
}

public sealed class ClusterImportResultVersionItem : ClusterImportResultItem
{
    public List<ClusterImportResultVersion> Versions { get; } = [];
}

public sealed class ClusterImportResultVersion : ClusterImportResultItem
{
    public required string CurrentVersion { get; set; }
    public required string ImportVersion { get; set; }

    public required string PackageName { get; set; }
}
