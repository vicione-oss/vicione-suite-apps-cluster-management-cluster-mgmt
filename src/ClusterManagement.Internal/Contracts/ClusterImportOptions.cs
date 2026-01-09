namespace ClusterManagement.Internal.Contracts;

public sealed class ClusterImportOptions
{
    public bool MergeNodeGroups { get; set; }
    public bool MergeNodes { get; set; }
    public bool MergeApplications { get; set; }
    public bool MergeEngineHosts { get; set; }
    public bool UpdateFunctionBlockVersions { get; set; }
}
