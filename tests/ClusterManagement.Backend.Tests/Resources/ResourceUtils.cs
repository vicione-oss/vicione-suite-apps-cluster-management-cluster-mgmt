using System.Reflection;
using System.Text.Json;
using Sdk.Backend.ArtifactApi;

namespace ClusterManagement.Backend.Tests.Resources;

internal static class ResourceUtils
{
    private const string ResourceLocation = "ClusterManagement.Backend.Tests.Resources";

    public static Task<IArtifactQueryResult> GetEmbeddedEngineHostQueryResult(CancellationToken cancellationToken = default)
        => GetEmbeddedEngineHostQueryResultInternal(k => k.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), cancellationToken);

    public static Task<IArtifactQueryResult> GetEmbeddedEngineHostMetadataQueryResult(CancellationToken cancellationToken = default)
        => GetEmbeddedEngineHostQueryResultInternal(k => k.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase), cancellationToken);

    /// <summary>
    /// Returns a real query result for all artifacts in jfrog/enginehost folder
    /// </summary>
    private static async Task<IArtifactQueryResult> GetEmbeddedEngineHostQueryResultInternal(Func<IArtifactItem, bool> acceptFilter, CancellationToken cancellationToken = default)
    {
        var result = await GetEmbeddedArtifactQueryResultInternal("jfrog_enginehost_result.json", cancellationToken);

        result.Results.RemoveAll(x => !acceptFilter(x));

        return result;
    }

    public static Task<IArtifactQueryResult> GetEmbeddedPackagesQueryResult(CancellationToken cancellationToken = default)
        => GetEmbeddedFunctionBlockQueryResult(k => k.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), cancellationToken);

    public static Task<IArtifactQueryResult> GetEmbeddedPackagesMetadataQueryResult(CancellationToken cancellationToken = default)
        => GetEmbeddedFunctionBlockQueryResult(k => k.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase), cancellationToken);


    /// <summary>
    /// Returns a shortened query result for all artifacts in jfrog/fbs folder 
    /// 29.09.2025 query without filter returns ~1.1Mb data!
    /// </summary>
    private static async Task<IArtifactQueryResult> GetEmbeddedFunctionBlockQueryResult(Func<IArtifactItem, bool> acceptFilter, CancellationToken cancellationToken = default)
    {
        var result = await GetEmbeddedArtifactQueryResultInternal("jfrog_fbs_result.json", cancellationToken);

        result.Results.RemoveAll(x => !acceptFilter(x));

        return result;
    }

    private static async Task<TestArtifactQueryResult> GetEmbeddedArtifactQueryResultInternal(string embeddedFileName, CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        ArgumentNullException.ThrowIfNull(assembly);

        using var stream = assembly.GetManifestResourceStream($"{ResourceLocation}.{embeddedFileName}");
        return await JsonSerializer.DeserializeAsync<TestArtifactQueryResult>(stream!, JsonSerializerOptions.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to load resource {embeddedFileName}");
    }
}
