using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterDependenciesConsumer(IDesignProvider designProvider, ILogger<GetClusterDependenciesConsumer> logger) :
    RequestConsumer<GetClusterDependenciesRequest, GetClusterDependenciesResponse>
{
    private readonly IDesignProvider _designProvider = designProvider;
    private readonly ILogger<GetClusterDependenciesConsumer> _logger = logger;

    protected override async Task<GetClusterDependenciesResponse> Respond(ConsumeContext<GetClusterDependenciesRequest> context)
        => new()
        {
            Dependencies = [.. await _designProvider.GetClusterDependencies()],
        };

    protected override Task<GetClusterDependenciesResponse> HandleException(ConsumeContext<GetClusterDependenciesRequest> context, Exception e)
    {
        _logger.LogError(e, "Consume {Request} failed", nameof(GetClusterDependenciesRequest));

        return Task.FromResult(new GetClusterDependenciesResponse
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
