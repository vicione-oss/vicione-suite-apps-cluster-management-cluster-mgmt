using System.Text.Json;
using Sdk.Backend.Artifacts;

namespace ClusterManagement.Backend.Tests.Extensions;

internal static class IArtifactRepositoryExtensions
{
    public static IArtifact Setup(this IArtifact artifact, string name, string path, DateTimeOffset? modified = null, long size = 1001L)
    {
        artifact.Name.Returns(name);
        artifact.Path.Returns(path);
        artifact.Modified.Returns(modified ?? DateTimeOffset.UtcNow);
        artifact.Size.Returns(size);

        return artifact;
    }

    public static async Task<MemoryStream> SetupMetadataDownload(this IArtifactRepository repository, IArtifact metadataArtifact, object downloadItem)
    {
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, downloadItem, JsonSerializerOptions.Default);
        ms.Seek(0, SeekOrigin.Begin);
        repository.Download(metadataArtifact).Returns(ms);

        return ms;
    }

    public static IArtifactQueryResult SetupExecuteQuery(this IArtifactRepository repository, string query, params IArtifact[] items)
    {
        var queryResult = Substitute.For<IArtifactQueryResult>();
        queryResult.Artifacts.Returns(items.AsReadOnly());

        var queryBuilder = repository.CreateQueryBuilder();
        queryBuilder.Build().Returns(query);

        repository.Query(query, Arg.Any<CancellationToken>())
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

    public static void SetupExecuteQuery(this IArtifactRepository repository, string query, IArtifactQueryResult queryResult)
    {
        var queryBuilder = repository.CreateQueryBuilder();
        queryBuilder.Build().Returns(query);

        repository.Query(query, Arg.Any<CancellationToken>())
            .Returns(queryResult);
    }
}
