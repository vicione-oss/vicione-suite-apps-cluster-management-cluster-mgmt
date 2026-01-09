namespace ClusterManagement.Backend.Engines;

public sealed class Container
{
    public Guid Id { get; set; }
    public Guid? ParentContainerId { get; set; }
    public string Name { get; set; } = string.Empty;
}
