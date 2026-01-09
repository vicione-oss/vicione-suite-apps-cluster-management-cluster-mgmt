using ClusterManagement.Client.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Services;

internal class ExportClusterService(IJsInterop jsInterop, ILogger<ExportClusterService> logger)
{
    private readonly IJsInterop _jsInterop = jsInterop;
    private readonly ILogger<ExportClusterService> _logger = logger;

    public async Task DownloadClusterJson(Cluster? cluster)
    {
        if (cluster is null)
            return;

        try
        {
            await _jsInterop.DownloadClusterJson(cluster);
        }
        catch (Exception e)
        {
            _logger.LogError(e, nameof(DownloadClusterJson));
        }
    }
}
