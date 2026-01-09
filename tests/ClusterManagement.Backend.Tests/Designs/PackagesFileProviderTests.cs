using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Designs;
using AwesomeAssertions;
using Semver;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackagesFileProvider_GetPackagesFilepath
{
    [Theory]
    [InlineData(null, $"{PackagesFileProvider.PackagesFileName}.{PackagesFileProvider.PackagesFileExtension}")]
    [InlineData("", $"{PackagesFileProvider.PackagesFileName}.{PackagesFileProvider.PackagesFileExtension}")]
    [InlineData("variant", $"{PackagesFileProvider.PackagesFileName}.variant.{PackagesFileProvider.PackagesFileExtension}")]
    [InlineData("edge-s", $"{PackagesFileProvider.PackagesFileName}.edge-s.{PackagesFileProvider.PackagesFileExtension}")]
    [InlineData("EDGE-m", $"{PackagesFileProvider.PackagesFileName}.edge-m.{PackagesFileProvider.PackagesFileExtension}")]
    public void Composes_filename_correctly(string? variant, string expected)
    {
        var packagesFilename = PackagesFileProvider.GetPackagesFilepath("basePath", variant, new MockFileSystem());
        Path.GetFileName(packagesFilename).Should().Be(expected);
    }
}

public class PackagesFileProvider_GetComponents
{
    [Fact]
    public void Returns_path_to_assembly()
    {
        MockFileSystem fileSystem = new();
        var functionBlock = fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("ifm.Add", "1.2.3"));
        fileSystem.AddFile(fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

        var components = PackagesFileProvider.GetComponents(fileSystem.Directory.GetCurrentDirectory(), fileSystem);

        var tempPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ifm.Add", "1.2.3");
        components.Should().ContainSingle().Which.Value.Should().Be(tempPath);
    }

    [Fact]
    public void Reads_version_of_FunctionBlock_if_available()
    {
        MockFileSystem fileSystem = new();
        var functionBlock = fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("ifm.Add", "1.2.3"));
        fileSystem.AddFile(fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

        var components = PackagesFileProvider.GetComponents(fileSystem.Directory.GetCurrentDirectory(), fileSystem);

        components.Should().ContainSingle().Which.Key.Version.ToString().Should().Be("1.2.3");
    }

    [Fact]
    public void Throws_if_directory_structure_is_suspicious()
    {
        MockFileSystem fileSystem = new();
        var functionBlock = fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("packages", "ifm.Add"));
        fileSystem.AddFile(fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

        var act = () => PackagesFileProvider.GetComponents(fileSystem.Directory.GetCurrentDirectory(), fileSystem).ToArray();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Returns_every_available_version()
    {
        MockFileSystem fileSystem = new();
        var functionBlockVersionOne = fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("ifm.Add", "1.0.0"));
        var functionBlockVersionTwo = fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine("ifm.Add", "2.0.0"));
        fileSystem.AddFile(fileSystem.Path.Combine(functionBlockVersionOne.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));
        fileSystem.AddFile(fileSystem.Path.Combine(functionBlockVersionTwo.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

        var components = PackagesFileProvider.GetComponents(fileSystem.Directory.GetCurrentDirectory(), fileSystem);

        var tempPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ifm.Add", "1.2.3");
        components.Should().HaveCount(2).And.SatisfyRespectively(
            e => e.Key.Version.Should().Be(new SemVersion(1, 0, 0)),
            e => e.Key.Version.Should().Be(new SemVersion(2, 0, 0)));
    }
}

public class PackagesFileProvider_ResolveLatestVersion
{
    [Fact]
    public void Returns_latest_version()
    {
        Dictionary<PackageInfo, string> components = new()
        {
            { new("A", new(1, 0, 0)), "path/a/1" },
            { new("A", new(2, 1, 0)), "path/a/2.1" },
            { new("A", new(2, 0, 0)), "path/a/2" },
            { new("B", new(1, 0, 0)), "path/b/1" },
        };

        var result = components.ResolveLatestVersion();

        result.Should().HaveCount(2).And.SatisfyRespectively(
            e =>
            {
                e.Key.Name.Should().Be("A");
                e.Key.Version.Should().Be(new SemVersion(2, 1, 0));
                e.Value.Should().Be("path/a/2.1");
            },
            e =>
            {
                e.Key.Name.Should().Be("B");
                e.Key.Version.Should().Be(new SemVersion(1, 0, 0));
                e.Value.Should().Be("path/b/1");
            });
    }
}
