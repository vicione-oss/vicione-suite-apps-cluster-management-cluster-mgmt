using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Backend.Designs;

internal record PackageLoadResult(
    IReadOnlyDictionary<Guid, FunctionBlockDesign> Designs,
    bool ContainsViciOneContent);
