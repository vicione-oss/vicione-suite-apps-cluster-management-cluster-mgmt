namespace ClusterManagement.Client.Management.Models;

public sealed record DeploymentErrorModel(Guid ApplicationId, int Code, string? Message);
