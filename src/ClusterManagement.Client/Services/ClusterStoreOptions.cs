namespace ClusterManagement.Client.Services;

public sealed record ClusterStoreOptions
{
    public bool CleanupDependencies { get; set; }
}
