using Microsoft.AspNetCore.Components;
using Sdk.Client.Extensions;

namespace ClusterManagement.Client.Management.Services;

internal interface IClusterSagaHttpClient
{
    Task ResetSaga(CancellationToken cancellationToken = default);
    Task<string> GetSagaState(CancellationToken token = default);
}

internal sealed class ClusterSagaHttpClient(HttpClient httpClient, NavigationManager navigationManager) : IClusterSagaHttpClient
{
    private const string Api = "api/cluster/saga";

    private readonly HttpClient _httpClient = httpClient;

    public Task ResetSaga(CancellationToken cancellationToken = default)
    {
        _httpClient.BaseAddress = navigationManager.ToAbsoluteUri(navigationManager.BaseUri);

        var builder = _httpClient.CreateUriBuilder().WithPath($"{Api}/reset");
        return _httpClient.GetAsync(builder.Uri, cancellationToken);
    }

    public async Task<string> GetSagaState(CancellationToken cancellationToken = default)
    {
        _httpClient.BaseAddress = navigationManager.ToAbsoluteUri(navigationManager.BaseUri);

        var builder = _httpClient.CreateUriBuilder().WithPath($"{Api}/status");
        return await _httpClient.GetStringAsync(builder.Uri, cancellationToken);
    }
}
