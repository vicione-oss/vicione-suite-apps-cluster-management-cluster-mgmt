namespace ClusterManagement.Client.Editor.Services;

internal interface IResourceDownloadStateService
{
    bool IsCompleted { get; }

    Task WaitForCompletion(CancellationToken cancellationToken = default);
}
