using Sdk.Instance;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Extensions;

public static class InstanceInformationExtensions
{
    public static List<CoreOsModule> GetViciOneCoreOSModules(this IInstanceInformation instance)
        => [.. instance.InstalledModules
            .Select(module =>
                new CoreOsModule
                {
                    Id = Guid.NewGuid(),
                    ModuleId = module
                })
            .OrderBy(m => m.ModuleId)];

    public static List<CoreOsModule> GetInternalViciOneCoreOSModules(this IInstanceInformation instance)
        => [.. instance.InstalledModules
            .Select(module =>
                new CoreOsModule
                {
                    Id = Guid.NewGuid(),
                    ModuleId = module
                })
            .OrderBy(m => m.ModuleId)];
}
