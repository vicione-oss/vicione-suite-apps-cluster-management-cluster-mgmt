using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterDependenciesConsumer(IDesignProvider designProvider, ILogger<GetClusterDependenciesConsumer> logger) :
    RequestConsumer<GetClusterDependenciesRequest, GetClusterDependenciesResponse>
{
    private readonly IDesignProvider _designProvider = designProvider;
    private readonly ILogger<GetClusterDependenciesConsumer> _logger = logger;

    public override async Task<GetClusterDependenciesResponse> Respond(GetClusterDependenciesRequest message, CancellationToken cancellationToken)
        => new()
        {
            Dependencies = [.. await _designProvider.GetClusterDependencies()],
        };

    public override Task<GetClusterDependenciesResponse> HandleException(GetClusterDependenciesRequest message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, "Consume {Request} failed", nameof(GetClusterDependenciesRequest));

        return Task.FromResult(new GetClusterDependenciesResponse
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
