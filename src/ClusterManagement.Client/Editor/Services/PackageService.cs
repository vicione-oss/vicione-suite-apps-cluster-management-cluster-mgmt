using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds.Commands;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Editor.Services;

internal class PackageService(IUiMediator mediator) : IPackageService
{
    private string _minCoreVersion = string.Empty;

    public string MinCoreVersion => _minCoreVersion;

    public async Task DeletePackage(string name, string version, CancellationToken cancellationToken = default)
        => await mediator.Send(new DeleteClusterPackage(name, version), cancellationToken);

    public async Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null, CancellationToken cancellationToken = default)
    {
        var response = await mediator.Request<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>(new GetClusterPackageInfosRequest(names), cancellationToken);

        _minCoreVersion = response.MinCoreVersion;

        return response.PackageInfos;
    }

    public async Task UploadPackage(byte[] data, CancellationToken cancellationToken = default)
        => await mediator.Send(new AddClusterPackage(data), cancellationToken);

    public async Task UpdatePackages(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken = default)
        => await mediator.Send(new UpdateClusterDependencies(operations), cancellationToken);
}
