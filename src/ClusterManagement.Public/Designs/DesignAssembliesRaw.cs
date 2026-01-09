namespace ClusterManagement.Public.Designs;

public sealed record DesignAssembliesRaw(Dictionary<string, byte[]> Dlls, Dictionary<string, byte[]> Pdbs, List<string> Ignored);
