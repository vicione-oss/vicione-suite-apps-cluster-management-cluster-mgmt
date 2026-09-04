using Sdk.Messaging;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

/// <summary>
/// Provides access to package contents including function block designs and rulesets.
/// </summary>
public interface IPackageContentProvider
{
    /// <summary>
    /// Gets all registered package contents.
    /// </summary>    
    IReadOnlyDictionary<ClusterDependency, PackageContent> GetPackageContents();

    /// <summary>
    /// Gets the package content for a specific cluster dependency.
    /// </summary>    
    PackageContent GetPackageContent(ClusterDependency dependency);

    /// <summary>
    /// Attempts to add a package from a directory containing function block designs and rulesets.
    /// </summary>
    /// <param name="dependency">The cluster dependency the package belongs to.</param>
    /// <param name="directory">The directory containing the function block designs and rulesets.</param>
    /// <param name="hide">
    /// <see langword="true"/> to mark the package content as hidden <see cref="PackageContent.Hide"/> ; otherwise, <see langword="false"/>.
    /// The default is <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    Task<bool> TryAddPackage(ClusterDependency dependency, string directory, bool hide = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to add a package from a collection of types and rulesets by generating function block designs at runtime.
    /// </summary>
    /// <param name="dependency">The cluster dependency the package belongs to.</param>
    /// <param name="types">The types the function block designs are generated from.</param>
    /// <param name="rulesets">The rulesets to register with the package.</param>
    /// <param name="hide">
    /// <see langword="true"/> to mark the package content as hidden <see cref="PackageContent.Hide"/> ; otherwise, <see langword="false"/>.
    /// The default is <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    Task<bool> TryAddPackage(ClusterDependency dependency, IReadOnlyCollection<Type> types, IReadOnlyCollection<Ruleset> rulesets, bool hide = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a package that failed to download or is missing from disk.
    /// The error is stored in <see cref="PackageContent.Error"/> so consumers can detect the broken state.
    /// </summary>
    void AddFailedPackage(ClusterDependency dependency, ErrorInfo error);
}
