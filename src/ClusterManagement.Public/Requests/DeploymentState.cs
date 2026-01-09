namespace ClusterManagement.Public.Requests;

public record DeploymentState(Guid ClusterId, string CurrentState, Version ActiveVersion, Version TargetVersion)
{
    // more details?    
}
