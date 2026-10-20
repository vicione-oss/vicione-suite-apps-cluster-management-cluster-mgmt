using System.ComponentModel.DataAnnotations;

namespace ClusterManagement.Internal;

/// <summary>
/// Maximum time the deploy cluster saga waits in a state before it gives up and rolls back or finalizes.
/// </summary>
public sealed class DeploySagaTimeoutOptions
{
    private const string MinTimeout = "00:00:01";
    private const string MaxTimeout = "1.00:00:00";

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan CommittingCluster { get; set; } = TimeSpan.FromMinutes(2);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan AddingClusterNodes { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan UpdatingDeployments { get; set; } = TimeSpan.FromMinutes(10);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan CleaningClusterNodes { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan CleaningCluster { get; set; } = TimeSpan.FromMinutes(2);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan RollingBackDeployments { get; set; } = TimeSpan.FromMinutes(10);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan RollingBackClusterNodes { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan RollingBackCommit { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How often a master or standalone instance checks for deployments whose state timed out
    /// </summary>
    [Range(typeof(TimeSpan), MinTimeout, MaxTimeout, ParseLimitsInInvariantCulture = true)]
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}
