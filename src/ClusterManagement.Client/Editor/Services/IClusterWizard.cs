using ViciOne.Cluster.Builder;

namespace ClusterManagement.Client.Editor.Services;

public interface IClusterWizard
{
    string Title { get; }
    Type ComponentType { get; }

    Task PrepareWizardOptions(ClusterBuilder builder);

    Task ProcessWizardOptions(ClusterBuilder builder);
}
