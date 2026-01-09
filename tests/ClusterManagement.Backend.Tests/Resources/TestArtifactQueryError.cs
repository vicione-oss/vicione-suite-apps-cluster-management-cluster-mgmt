using Sdk.Backend.Artifacts;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Resources;

internal sealed class TestArtifactQueryError : IArtifactQueryError
{
    public string Source { get; set; } = string.Empty;

    public required ErrorInfo Error { get; set; }
}
