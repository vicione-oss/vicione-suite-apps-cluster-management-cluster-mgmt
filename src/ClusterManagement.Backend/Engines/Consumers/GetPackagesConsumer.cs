using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Internal.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Engines.Consumers;

[ReadOnlyConsumer]
public sealed class GetPackagesConsumer(IEngineHostCommunication engineHostCommunication, IEngineDbContext engineDbContext)
    : InstanceDependentRequestConsumer<GetPackagesOnInstanceRequest, GetPackagesOnInstanceResponse>
{
    private readonly IEngineHostCommunication _engineHostCommunication = engineHostCommunication;
    private readonly IEngineDbContext _engineDbContext = engineDbContext;

    public override Task<GetPackagesOnInstanceResponse> HandleException(GetPackagesOnInstanceRequest context, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetPackagesOnInstanceResponse(new ErrorInfo(((ErrorInfoException)e).ErrorCode, e.Message)));

    public override async Task<GetPackagesOnInstanceResponse> Respond(GetPackagesOnInstanceRequest context, CancellationToken cancellationToken)
    {
        var deployment = await _engineDbContext.EngineDeployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EngineId == context.EngineId, cancellationToken);
        if (deployment is not null)
        {
            try
            {
                var packages = await _engineHostCommunication.GetPackages(deployment.EngineHostId, deployment.Id, cancellationToken);

                return new GetPackagesOnInstanceResponse() { Packages = packages.ToDictionary(p => p.Name, p => p.Version) };
            }
            catch (Exception ex)
            {
                throw new ErrorInfoException($"Cannot retrieve packages for engine '{context.EngineId}'.", ex);
            }
        }

        throw new ErrorInfoException($"Cannot find engine '{context.EngineId}'.");
    }
}
