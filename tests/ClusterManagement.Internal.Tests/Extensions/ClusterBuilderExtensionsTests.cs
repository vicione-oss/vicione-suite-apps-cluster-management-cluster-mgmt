using ClusterManagement.Internal.Extensions;
using ClusterManagement.Tests.Tools.Factories;

namespace ClusterManagement.Internal.Tests.Extensions;

public class ClusterBuilderExtensionsTests
{
    public class GetAllNestedDataPortTreeNodes : ClusterBuilderExtensionsTests
    {
        [Fact(Skip = "TODO - add tests for ClusterBuilderExtensions")]
        public void Should_return_all_input_connectors()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster();

            // Act
            var inputs = builder.Cluster.GetAllNestedDataPortTreeNodes();

            // Assert
            builder.Cache.Connectors.Should().NotBeEmpty();
        }
    }
}
