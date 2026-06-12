using ClusterManagement.Backend.Contracts;
using Sdk.Backend.Artifacts;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

/// <summary>
/// Provides methods for downloading and querying package artifacts within a cluster.
/// </summary>
public interface IPackageArtifactRepository
{
    /// <summary>
    /// Downloads all cluster packages specified by a package manifest and extracts them to the local file system.
    /// </summary>
    /// <param name="dependencyFile">The path to the package manifest file.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an array of <see cref="PackageDownloadResult"/> values representing the results of the download and extraction operation.
    /// </returns>
    Task<PackageDownloadResult[]> DownloadAndExtract(string dependencyFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads all cluster packages specified by <param name="dependencies"> and extracts them to the local file system.
    /// </summary>
    /// <param name="dependencies">The collection of packages to download.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an array of <see cref="PackageDownloadResult"/> values representing the results of the download and extraction operation.
    /// </returns>
    Task<PackageDownloadResult[]> DownloadAndExtract(IReadOnlyCollection<ClusterDependency> dependencies, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the specified cluster package by name and version and extracts it to the local file system.
    /// </summary>
    /// <param name="functionBlockName">The name of the package to download.</param>
    /// <param name="version">The version of the package to download.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an <see cref="PackageDownloadResult"/> representing the result of the download and extraction operation.
    /// </returns>
    Task<PackageDownloadResult> DownloadAndExtract(string functionBlockName, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the latest ZIP package artifact for the specified core version.
    /// </summary>
    /// <param name="coreVersion">The core version that all candidate artifacts must support.</param>
    /// <param name="packageName">The name of the package to query.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns the latest <see cref="ZipArtifact"/> that supports <paramref name="coreVersion"/>,
    /// or <see langword="null"/> if there are no matching results.
    /// </returns>
    /// <example>
    /// <code>
    /// await api.QueryLatestZipArtifact(new SemVersion(0, 48, 0), "ViciOne.Math", CancellationToken.None);
    /// </code>
    /// </example>
    Task<ZipArtifact?> QueryLatestZipArtifact(SemVersion coreVersion, string packageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all cluster package metadata artifacts (JSON), or only artifacts supporting a given core version.
    /// Use <paramref name="packageName"/> to filter by a specific package.
    /// </summary>
    /// <param name="coreVersion">The core version to filter by, or <see langword="null"/> to return all artifacts.</param>
    /// <param name="packageName">The package name to filter by, or <see langword="null"/> to include all packages.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an <see cref="IReadOnlyCollection{T}"/> of <see cref="IArtifact"/> representing the metadata artifacts.
    /// </returns>
    Task<IReadOnlyCollection<IArtifact>> QueryMetadataArtifacts(SemVersion? coreVersion, string? packageName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all cluster package metadata artifacts (JSON), or only artifacts supporting a given core version.
    /// Use <paramref name="packageNames"/> to filter by a list of specific packages.
    /// </summary>
    /// <param name="packageNames">The package names to filter by.</param>
    /// <param name="coreVersion">The core version to filter by, or <see langword="null"/> to return all artifacts.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an <see cref="IReadOnlyCollection{T}"/> of <see cref="IArtifact"/> representing the metadata artifacts.
    /// </returns>
    Task<IReadOnlyCollection<IArtifact>> QueryMetadataArtifacts(IReadOnlyCollection<string> packageNames, SemVersion? coreVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the ZIP package artifact related to the specified metadata artifact (JSON).
    /// </summary>
    /// <param name="metadataArtifact">
    /// The metadata artifact to query (for example, an artifact item like <c>0.28.0-linux-x64_0.48.0.json</c>).
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns the <see cref="ZipArtifact"/> enriched with the package metadata.
    /// </returns>
    Task<ZipArtifact> QueryZipArtifact(IArtifact metadataArtifact, CancellationToken cancellationToken = default);
}
