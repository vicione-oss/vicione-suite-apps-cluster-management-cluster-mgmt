namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class ContainerAssignment
{
    public Container Container { get; set; } = new();
    public List<Container> Containers { get; init; } = [];
}
