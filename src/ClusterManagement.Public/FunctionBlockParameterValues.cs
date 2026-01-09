namespace ClusterManagement.Public;

public sealed record FunctionBlockParameterValues(Guid FunctionBlockId, IDictionary<Guid, FunctionBlockParameterValue> ParametersValue);
