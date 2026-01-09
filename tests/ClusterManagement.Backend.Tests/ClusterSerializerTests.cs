using System.Text.Json;
using ClusterManagement.Public;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using AwesomeAssertions;
using ViciOne.Cluster.Builder;
using Xunit;

namespace ClusterManagement.Backend.Tests;

public class ClusterSerializerTests
{
    private readonly JsonSerializerOptions _serializerOptions = ClusterSerializer.CreateSerializerOptions();

    public sealed class Compress_Cluster : ClusterSerializerTests
    {
        [Fact]
        public void Should_compress_cluster()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

            // Act
            var compressed = ClusterSerializer.Compress(builder.Cluster);

            // Assert
            compressed.Length.Should().BeGreaterThan(100);
        }
    }

    public sealed class Compress_String
    {
        [Fact]
        public void Should_compress_cluster()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
            var json = ClusterSerializer.Serialize(builder.Cluster);

            // Act
            var compressed = ClusterSerializer.Compress(json);

            // Assert
            compressed.Length.Should().BeGreaterThan(100);
        }
    }

    public sealed class Decompress : ClusterSerializerTests
    {
        [Fact]
        public async Task Should_decompress_cluster()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
            var compressed = ClusterSerializer.Compress(builder.Cluster, _serializerOptions);

            // Act
            var decompressed = await ClusterSerializer.Decompress(compressed, _serializerOptions);

            // Assert
            using var compareBuilder = new ClusterBuilder(decompressed, builder.DependencyResolver);
            compareBuilder.CacheShouldBeEquivalent(builder);
        }
    }

    public sealed class Deserialize : ClusterSerializerTests
    {
        [Fact]
        public void Should_deserialize_cluster()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);
            var json = ClusterSerializer.Serialize(builder.Cluster, _serializerOptions);

            // Act
            var cluster = ClusterSerializer.Deserialize(json, _serializerOptions);

            // Assert
            using var compareBuilder = new ClusterBuilder(cluster, builder.DependencyResolver);
            compareBuilder.CacheShouldBeEquivalent(builder);
        }
    }

    public sealed class Serialize : ClusterSerializerTests
    {
        [Fact]
        public void Should_serialize_cluster()
        {
            // Arrange
            using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Ultra);

            // Act
            var json = ClusterSerializer.Serialize(builder.Cluster, _serializerOptions);

            // Assert
            json.Should().NotBeNullOrWhiteSpace();
        }
    }
}
