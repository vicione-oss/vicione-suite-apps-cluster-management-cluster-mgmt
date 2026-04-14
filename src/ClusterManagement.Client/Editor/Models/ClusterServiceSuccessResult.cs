using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;

namespace ClusterManagement.Client.Editor.Models;

/// <summary>
/// Describes the result of a successful call to a method of <see cref="IPackageServiceResult"/> 
/// </summary>
public readonly record struct ClusterServiceSuccessResult : IClusterServiceResult;
