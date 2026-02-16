using Sdk.Testing;

namespace ClusterManagement.Backend.Tests;

public class MetadataTests
{
    [Fact]
    public void Should_be_serializable()
        => MetadataValidator.ValidateMetadata();
}
