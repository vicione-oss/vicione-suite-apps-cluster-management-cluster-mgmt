using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;

namespace ClusterManagement.Client.Editor.Models;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IPackageServiceResult"/> 
/// </summary>
public readonly record struct ClusterServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IClusterServiceResult;
