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

    public async Task DeletePackage(string name, string version)
        => await mediator.Send(new DeleteClusterPackage(name, version));

    public async Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null)
    {
        var response = await mediator.Request<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>(new GetClusterPackageInfosRequest(names));

        _minCoreVersion = response.MinCoreVersion;

        return response.PackageInfos;
    }

    public async Task UploadPackage(byte[] data)
        => await mediator.Send(new AddClusterPackage(data));

    public async Task UpdatePackages(List<ClusterDependencyOperation> operations)
        => await mediator.Send(new UpdateClusterDependencies(operations));
}
