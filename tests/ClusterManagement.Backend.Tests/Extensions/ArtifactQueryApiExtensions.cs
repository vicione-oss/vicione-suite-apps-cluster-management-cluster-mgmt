using System.Text.Json;
using NSubstitute;
using Sdk.Backend.ArtifactApi;

namespace ClusterManagement.Backend.Tests.Extensions;

internal static class ArtifactQueryApiExtensions
{
    public static IArtifactItem Setup(this IArtifactItem artifactItem, string name, string path, DateTimeOffset? modified = null, long size = 1001L)
    {
        artifactItem.Name = name;
        artifactItem.Path = path;
        artifactItem.Modified = modified ?? DateTimeOffset.UtcNow;
        artifactItem.Size = size;

        return artifactItem;
    }

    public static async Task<MemoryStream> SetupMetadataDownload(this IArtifactQueryApi api, IArtifactItem metadataArtifact, object downloadItem)
    {
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, downloadItem, JsonSerializerOptions.Default);
        ms.Seek(0, SeekOrigin.Begin);
        api.DownloadStream(metadataArtifact).Returns(ms);

        return ms;
    }

    public static IArtifactQueryResult SetupExecuteQuery(this IArtifactQueryApi api, string query, params IArtifactItem[] items)
    {
        var queryResult = Substitute.For<IArtifactQueryResult>();
        queryResult.Artifacts.Returns(items.AsReadOnly());

        var queryBuilder = api.CreateQueryBuilder();
        queryBuilder.BuildQueryString().Returns(query);

        api.ExecuteQuery(query, Arg.Any<CancellationToken>())
            .Returns(queryResult);

        return queryResult;
    }

    public static void SetupMethodReturns(this IArtifactQueryBuilder queryBuilder)
    {
        // ensure these calls return always the same instance
        queryBuilder.AndPathMatches(Arg.Any<string>()).Returns(queryBuilder);
        queryBuilder.AndNameMatches(Arg.Any<string>()).Returns(queryBuilder);
        queryBuilder.AndNameNotMatches(Arg.Any<string>()).Returns(queryBuilder);
        queryBuilder.OrderByDescending(Arg.Any<string[]>()).Returns(queryBuilder);
        queryBuilder.IncludeFields(Arg.Any<string[]>()).Returns(queryBuilder);
    }

    public static void SetupExecuteQuery(this IArtifactQueryApi api, string query, IArtifactQueryResult queryResult)
    {
        var queryBuilder = api.CreateQueryBuilder();
        queryBuilder.BuildQueryString().Returns(query);

        api.ExecuteQuery(query, Arg.Any<CancellationToken>())
            .Returns(queryResult);
    }
}
