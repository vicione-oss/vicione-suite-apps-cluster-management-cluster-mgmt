using ClusterManagement.Internal.Extensions;
using Sdk.Instance;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Factories;

public static class ClusterNodeFactory
{
    public static ClusterNode CreateApplicationNode(IInstanceInformation instance, bool isRunningInContainer = true)
        => new()
        {
            Id = Guid.NewGuid(),
            // BaseImage, => Discuss w/ infrastructure (TODO)
            Type = isRunningInContainer
                ? ClusterNodeType.Container
                : ClusterNodeType.Physical,
            Name = instance.Name ?? string.Empty,
            Description = instance.Description,
            Applications =
            {
                new ClusterApplication
                {
                    Id = instance.Id,
                    Name = instance.Name ?? string.Empty,
                    Description = instance.Description,
                    Type = instance.Type.GetCoreOsType(),
                    CoreOsModules = instance.GetViciOneCoreOSModules(),
                }
            }
        };
}
