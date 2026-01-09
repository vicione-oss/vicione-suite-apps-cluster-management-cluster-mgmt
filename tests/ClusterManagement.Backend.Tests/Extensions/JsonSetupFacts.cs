using System.Text.Json.Serialization;
using ClusterManagement.Backend.Extensions;
using AwesomeAssertions;
using Xunit;

namespace ClusterManagement.Backend.Tests.Extensions;

public sealed class JsonSetupFacts
{
    public sealed class DefaultOptions
    {
        [Fact]
        public void Contains_JsonStringEnumConverter()
            => JsonSetup.DefaultOptions.Converters.Should().ContainSingle().Which.Should().BeOfType<JsonStringEnumConverter>();

        [Fact]
        public void Ignores_read_only_members()
        {
            var options = JsonSetup.DefaultOptions;

            options.IgnoreReadOnlyFields.Should().BeTrue();
            options.IgnoreReadOnlyProperties.Should().BeTrue();
        }

        [Fact]
        public void Allows_named_floating_point_literals()
            => JsonSetup.DefaultOptions.NumberHandling.Should().Be(JsonNumberHandling.AllowNamedFloatingPointLiterals);
    }

    public sealed class PreserveTypeOptions
    {
        [Fact]
        public void Contains_JsonStringEnumConverter()
            => JsonSetup.PreserveTypeOptions.Converters.Should().ContainItemsAssignableTo<JsonStringEnumConverter>();

        [Fact]
        public void Contains_TypeJsonConverter()
            => JsonSetup.PreserveTypeOptions.Converters.Should().ContainItemsAssignableTo<TypeJsonConverter>();

        [Fact]
        public void Ignores_read_only_members()
        {
            var options = JsonSetup.PreserveTypeOptions;

            options.IgnoreReadOnlyFields.Should().BeTrue();
            options.IgnoreReadOnlyProperties.Should().BeTrue();
        }

        [Fact]
        public void Allows_named_floating_point_literals()
            => JsonSetup.PreserveTypeOptions.NumberHandling.Should().Be(JsonNumberHandling.AllowNamedFloatingPointLiterals);

        [Fact]
        public void Preserves_references()
            => JsonSetup.PreserveTypeOptions.ReferenceHandler.Should().BeSameAs(ReferenceHandler.Preserve);
    }
}
