using ClusterManagement.Public.Designs;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Tests.Tools.Factories;

public static class ClusterDependencyPackageFactory
{
    public static List<ClusterDependencyPackage> CreateDependencyDesigns(IEnumerable<ViciOne.Core.Dataflow.DataModel.FunctionBlockDesign> designs)
        =>
        [
            new ClusterDependencyPackage(new ClusterDependency
                {
                    Name = "TestDependency",
                    Version = "0.0.1"
                },
                [.. designs],
                []),
            new ClusterDependencyPackage(new ClusterDependency
                {
                    Name = "MqttDataPortDesign",
                    Version = "0.0.1"
                },
                [], [Public.Constants.MqttDataPortDesignId])
        ];
}
