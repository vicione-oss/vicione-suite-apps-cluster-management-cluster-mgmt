using Sdk.Testing;
using Xunit;

namespace ClusterManagement.Backend.Tests;

public class MetadataTests
{
    [Fact]
    public void Should_be_serializable()
        => MetadataValidator.ValidateMetadata();
}
