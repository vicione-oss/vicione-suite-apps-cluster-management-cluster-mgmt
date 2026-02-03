using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Contracts;

public record ClusterDependencyOperation(ClusterDependency Dependency, ClusterDependencyOperationKind OperationKind);
