using ClusterManagement.Public;
using Sdk.Client.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Extensions;

internal static class JsInteropExtensions
{
    public static Task DownloadClusterJson(this IJsInterop jsInterop, Cluster cluster)
        => jsInterop.DownloadAs(ClusterSerializer.Serialize(cluster), GetClusterJsonFileName(cluster));

    private static string GetClusterJsonFileName(Cluster cluster)
        => $"{cluster.Id}-{cluster.Version}.json";
}
