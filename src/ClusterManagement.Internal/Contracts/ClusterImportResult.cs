namespace ClusterManagement.Internal.Contracts;

public sealed class ClusterImportResult
{
    public ClusterImportResultVersionItem Dependencies { get; } = new();
    public ClusterImportResultItem NodeGroups { get; } = new();
    public ClusterImportResultItem Nodes { get; } = new();
    public ClusterImportResultItem Applications { get; } = new();
    public ClusterImportResultItem EngineHosts { get; } = new();
    public ClusterImportResultItem Engines { get; } = new();
    public ClusterImportResultItem Dataflows { get; } = new();
    public ClusterImportResultItem Containers { get; } = new();
    public ClusterImportResultItem ContainerConnectors { get; } = new();
    public ClusterImportResultItem FunctionBlocks { get; } = new();
    public ClusterImportResultItem FunctionBlockDesigns { get; } = new();
    public ClusterImportResultItem Labels { get; } = new();
    public ClusterImportResultItem Links { get; } = new();
    public ClusterImportResultItem DataPorts { get; } = new();
    public ClusterImportResultItem DataPortTreeNodes { get; } = new();
    public ClusterImportResultItem DataPortProperties { get; } = new();

    public int TotalSkipped => Dependencies.Skipped
        + NodeGroups.Skipped
        + Nodes.Skipped
        + Applications.Skipped
        + EngineHosts.Skipped
        + Engines.Skipped
        + Dataflows.Skipped
        + Containers.Skipped
        + ContainerConnectors.Skipped
        + FunctionBlocks.Skipped
        + Labels.Skipped
        + Links.Skipped
        + DataPorts.Skipped
        + DataPortTreeNodes.Skipped
        + DataPortProperties.Skipped;

    public int TotalAdded => Dependencies.Added
        + NodeGroups.Added
        + Nodes.Added
        + Applications.Added
        + EngineHosts.Added
        + Engines.Added
        + Dataflows.Added
        + Containers.Added
        + ContainerConnectors.Added
        + FunctionBlocks.Added
        + Labels.Added
        + Links.Added
        + DataPorts.Added
        + DataPortTreeNodes.Added
        + DataPortProperties.Added;

    public int TotalMerged => Dependencies.Merged
        + NodeGroups.Merged
        + Nodes.Merged
        + Applications.Merged
        + EngineHosts.Merged
        + Engines.Merged
        + Dataflows.Merged
        + Containers.Merged
        + ContainerConnectors.Merged
        + FunctionBlocks.Merged
        + Labels.Merged
        + Links.Merged
        + DataPorts.Merged
        + DataPortTreeNodes.Merged
        + DataPortProperties.Merged;

    public IEnumerable<string> TotalErrors => new[] {
        Dependencies.Errors.ToArray(),
        [.. NodeGroups.Errors],
        [.. Applications.Errors],
        [.. EngineHosts.Errors],
        [.. Engines.Errors],
        [.. Dataflows.Errors],
        [.. Containers.Errors],
        [.. ContainerConnectors.Errors],
        [.. FunctionBlocks.Errors],
        [.. Labels.Errors],
        [.. Links.Errors],
        [.. DataPorts.Errors],
        [.. DataPortTreeNodes.Errors],
        [.. DataPortProperties.Errors] }.SelectMany(k => k);
}
