namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Models;

/// <summary>
/// Describes the result of a <see cref="IPackageService"/> method call
/// </summary>
public interface IPackageServiceResult;

/// <summary>
/// Describes the result of a successful call to a method of <see cref="IPackageServiceResult"/> 
/// </summary>
public readonly record struct PackageServiceSuccessResult : IPackageServiceResult;


/// <summary>
/// Describes the result of a failed call to a method of <see cref="IPackageServiceResult"/> 
/// </summary>
public readonly record struct PackageServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IPackageServiceResult;
