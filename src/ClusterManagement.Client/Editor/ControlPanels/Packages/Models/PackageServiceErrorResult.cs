namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Models;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IPackageServiceResult"/> 
/// </summary>
public readonly record struct PackageServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IPackageServiceResult;
