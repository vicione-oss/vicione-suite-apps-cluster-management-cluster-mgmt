using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClusterManagement.Backend.Extensions;

internal static class ModelBuilderExtensions
{
    internal static PropertyBuilder<Version> PersistAsString(this PropertyBuilder<Version> propertyBuilder)
        => propertyBuilder.HasConversion(
            v => v.ToString(),
            v => new Version(v),
            CreateVersionComparer());

    private static ValueComparer<Version> CreateVersionComparer()
        => new((v1, v2) => Equals(v1, v2),
            v => v.GetHashCode(),
            v => new Version(v.ToString()));
}
