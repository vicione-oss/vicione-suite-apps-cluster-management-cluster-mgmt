using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests;

public static class TestDataFactory
{
    private static readonly Random s_random = new();

    public static ClusterDependency ClusterDependencyMathAdd => new()
    {
        Name = "ACX.StandardFunction.Math.Add",
        Version = new Version("0.32.0"),
    };

    public static List<ClusterInfo> CreateClusterInfos(params Guid[] guids)
    {
        var list = new List<ClusterInfo>();

        for (var i = 0; i < guids.Length; i++)
        {
            var versionCount = s_random.Next(1, 10);
            for (var j = 0; j < versionCount; j++)
            {
                var major = j % 2 == 0 ? s_random.Next(0, 3) : 1;

                list.Add(new(guids[i], new Version(major, s_random.Next(0, 9), s_random.Next(0, 9)), $"Cluster{j} {i}"));
            }
        }

        return list;
    }

    public static List<ClusterInfoModel> CreateClusterViewModels(params Guid[] guids)
        => [.. CreateClusterInfos(guids).Select(c => new ClusterInfoModel(c))];
}
