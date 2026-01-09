using System.ComponentModel;
using Sdk.Instance;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Extensions;

public static class InstanceTypeExtensions
{
    public static NetworkType GetNetworkType(this InstanceType instanceType)
        => instanceType switch
        {
            InstanceType.Master => NetworkType.Mesh,
            InstanceType.Slave => NetworkType.Mesh,
            InstanceType.Standalone => NetworkType.Single,
            _ => throw new InvalidEnumArgumentException(nameof(instanceType), (int)instanceType, typeof(InstanceType))
        };

    public static ClusterApplicationType GetCoreOsType(this InstanceType instanceType)
        => instanceType switch
        {
            InstanceType.Master => ClusterApplicationType.CoreOsMaster,
            InstanceType.Slave => ClusterApplicationType.CoreOsSlave,
            InstanceType.Standalone => ClusterApplicationType.CoreOsStandalone,
            _ => throw new InvalidEnumArgumentException(nameof(instanceType), (int)instanceType, typeof(InstanceType))
        };

    public static bool IsMessageBroker(this InstanceType instanceType)
        => instanceType != InstanceType.Slave;

    public static bool HasMasterDatabase(this InstanceType instanceType)
        => instanceType == InstanceType.Master;
}
