namespace ClusterManagement.Public.Requests;

public record DeploymentState(Guid ClusterId, int State, Version ActiveVersion, Version TargetVersion)
{
    // more details?    
}
