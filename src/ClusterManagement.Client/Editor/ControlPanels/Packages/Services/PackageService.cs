using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds.Commands;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

internal class PackageService : CompletionSourceHandlerBase<IPackageServiceResult>,
    IPackageService,
    IEventConsumer<ClusterPackageChanged>,
    IEventConsumer<ClusterDependenciesChanged>
{
    private string _minCoreVersion = string.Empty;

    public string MinCoreVersion => _minCoreVersion;

    public event Func<ClusterPackageChanged, CancellationToken, Task>? PackageChanged;

    public event Func<ClusterDependenciesChanged, CancellationToken, Task>? DependenciesChanged;

    public PackageService(IUiMediator mediator) : base(mediator)
    {
        Register<ClusterPackageChanged>();
        Register<ClusterDependenciesChanged>();
    }

    public async Task<IPackageServiceResult> DeletePackage(string name, string version, CancellationToken cancellationToken = default)
    {
        var command = new DeleteClusterPackage(name, version);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null, CancellationToken cancellationToken = default)
    {
        var response = await Mediator.Request<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>(new GetClusterPackageInfosRequest(names), cancellationToken);

        _minCoreVersion = response.MinCoreVersion;

        return response.PackageInfos;
    }

    public async Task<IPackageServiceResult> UploadPackage(byte[] data, CancellationToken cancellationToken = default)
    {
        var command = new AddClusterPackage(data);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IPackageServiceResult> UpdatePackages(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken = default)
    {
        var command = new UpdateClusterDependencies(operations);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<ClusterPackageChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        try
        {
            await NotifyPackageChanged(context.Message, cancellationToken);
        }
        finally
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }
    }

    public async Task Consume(ClientContext<ClusterDependenciesChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        try
        {
            await NotifyDependenciesChanged(context.Message, cancellationToken);
        }
        finally
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }
    }

    protected override IPackageServiceResult CreateSuccessResult()
        => new PackageServiceSuccessResult();

    protected override IPackageServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new PackageServiceErrorResult(errorMessage, errorCode);

    private async Task NotifyPackageChanged(ClusterPackageChanged change, CancellationToken cancellationToken)
    {
        if (PackageChanged is not null)
            await PackageChanged.Invoke(change, cancellationToken);
    }

    private async Task NotifyDependenciesChanged(ClusterDependenciesChanged change, CancellationToken cancellationToken)
    {
        if (DependenciesChanged is not null)
            await DependenciesChanged.Invoke(change, cancellationToken);
    }
}
