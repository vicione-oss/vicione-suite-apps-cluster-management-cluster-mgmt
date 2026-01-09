using ViciOne.Cluster.Builder;

namespace ClusterManagement.Client.Editor.Services;

public interface IClusterBuilderProvider
{
    ClusterBuilder? Builder { get; }
    bool AllowModify { get; }

    event EventHandler? ClusterLoaded;
}
