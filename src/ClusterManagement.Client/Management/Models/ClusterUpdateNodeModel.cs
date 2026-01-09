namespace ClusterManagement.Client.Management.Models;

public sealed class ClusterUpdateNodeModel : ClusterUpdateModel
{
    public bool NeedCleanup { get; set; }
    public bool NeedCreation { get; set; }

    public ClusterUpdateApplicationModel? CoreOS { get; init; }
}
