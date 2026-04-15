using ClusterManagement.Client.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Services;

internal class ExportClusterService(IJsInterop jsInterop, ILogger<ExportClusterService> logger)
{
    public async Task DownloadClusterJson(Cluster? cluster)
    {
        if (cluster is null)
            return;

        try
        {
            await jsInterop.DownloadClusterJson(cluster);
        }
        catch (Exception e)
        {
            logger.LogFailure(e, nameof(DownloadClusterJson));
        }
    }
}
