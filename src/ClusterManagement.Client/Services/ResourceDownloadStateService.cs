using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ClusterManagement.Public.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Services;

internal sealed class ResourceDownloadStateService : IResourceDownloadStateService, IEventConsumer<ResourcesDownloaded>, IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly IDisposable _subscription;
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsCompleted => _completionSource.Task.IsCompleted;

    public ResourceDownloadStateService(IUiMediator mediator)
    {
        _mediator = mediator;
        _subscription = mediator.Register(this);
    }

    public async Task WaitForCompletion(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return;

        var response = await _mediator.Request<GetResourcesDownloadStatusRequest, GetResourcesDownloadStatusResponse>(
            new GetResourcesDownloadStatusRequest(), cancellationToken);

        if (response.IsCompleted)
        {
            _completionSource.TrySetResult();
            return;
        }

        await _completionSource.Task.WaitAsync(cancellationToken);
    }

    public Task Consume(ClientContext<ResourcesDownloaded> context, CancellationToken cancellationToken)
    {
        _completionSource.TrySetResult();
        return Task.CompletedTask;
    }

    public void Dispose() => _subscription.Dispose();
}
