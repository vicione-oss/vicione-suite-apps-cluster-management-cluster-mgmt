using Sdk.Backend.Artifacts;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Resources;

internal sealed class TestArtifactQueryResult : IArtifactQueryResult
{
    public List<TestArtifact> Results { get; set; } = [];
    public ErrorInfo? ErrorInfo { get; set; }
    public List<TestArtifactQueryRange>? RangesWrapper { get; set; }

    public List<TestArtifactQueryError>? ErrorsWrapper { get; set; }

    public IReadOnlyCollection<IArtifactQueryRange>? Ranges => RangesWrapper;

    public IReadOnlyCollection<IArtifactQueryError>? Errors => ErrorsWrapper;

    public IReadOnlyCollection<IArtifact> Artifacts => Results;
}
