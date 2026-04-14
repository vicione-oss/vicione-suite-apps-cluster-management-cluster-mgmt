namespace ClusterManagement.Public.Services;

public interface IResourceDownloadStateService
{
    bool IsCompleted { get; }

    Task WaitForCompletion(CancellationToken cancellationToken = default);
}
