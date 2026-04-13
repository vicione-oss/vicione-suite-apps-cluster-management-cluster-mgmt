namespace ClusterManagement.Public;

/// <summary>
/// Represents a service responsible for downloading necessary resources.
/// </summary>
public interface IDownloader
{
    /// <summary>
    /// Task that completes when the download process is finished.
    /// </summary>
    Task Task { get; }
}
