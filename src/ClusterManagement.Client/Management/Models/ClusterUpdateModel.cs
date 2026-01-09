using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Models;

public abstract class ClusterUpdateModel
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public ErrorInfo? Error { get; set; }
    public bool Processed { get; set; }
}
