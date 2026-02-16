using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Designs;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Designs;

public class ClusterAssembliesResolver_GetPackages
{
    [Fact]
    public void Returns_content_of_packages()
    {
        ClusterDependency[] dependencies =
        [
            new()
            {
                Name = "A",
                Version = new Version("1.2.3"),
            },
            new()
            {
                Name = "B",
                Version = new Version("2.0.0"),
            },
        ];
        MockFileSystem fileSystem = new();
        fileSystem.AddDirectory("packages");
        fileSystem.AddDirectory("packages/A/1.2.3");
        fileSystem.AddDirectory("packages/A/1.2.3/lib");
        fileSystem.AddFile("packages/A/1.2.3/A.dll", new MockFileData([]));
        fileSystem.AddFile("packages/A/1.2.3/A.json", new MockFileData([]));
        fileSystem.AddFile("packages/A/1.2.3/lib/Interop.dll", new MockFileData([]));
        fileSystem.AddFile("packages/B/2.0.0/hello.dll", new MockFileData([]));

        var packages = ClusterAssembliesResolver.GetPackages(dependencies, "packages", fileSystem);

        packages.Should().HaveCount(2);

        packages.SelectMany(p => p.Assets)
            .Should()
                .HaveCount(4).And.Subject
            .Select(a => Path.GetFileName(a.Filename))
            .Should()
                .Contain("A.dll").And
                .Contain("A.json").And
                .Contain("Interop.dll").And
                .Contain("hello.dll");
    }
}

public class ClusterAssembliesResolver_ByHighestVersionOrLastFile
{
    private readonly ClusterAssembliesResolver.Package _package = new(string.Empty, []);

    [Fact]
    public void Returns_last_file_if_no_version_is_available()
    {
        ClusterAssembliesResolver.AssetFileCandidate[] assets =
        [
            new("file", default, _package),
            new("file", default, _package),
            new("file", default, _package),
        ];
        MockFileSystem fileSystem = new();

        var result = assets.Aggregate((a1, a2) => ClusterAssembliesResolver.ByHighestVersionOrLastFile(a1, a2, fileSystem));

        result.Should().BeSameAs(assets[2]);
    }

    [Fact]
    public void Returns_file_with_highest_version()
    {
        ClusterAssembliesResolver.AssetFileCandidate[] assets =
        [
            new("A", new("1.0.0"), _package),
            new("B", new("1.1.1"), _package),
            new("C", new("1.1.0"), _package),
        ];
        MockFileSystem fileSystem = new();

        var result = assets.Aggregate((a1, a2) => ClusterAssembliesResolver.ByHighestVersionOrLastFile(a1, a2, fileSystem));

        result.Should().BeSameAs(assets[1]);
    }

    [Fact]
    public void Returns_first_file_of_equal_versioned_assemblies()
    {
        ClusterAssembliesResolver.AssetFileCandidate[] assets =
        [
            new("A", new("1.1.1"), _package),
            new("B", new("1.1.1"), _package),
        ];
        MockFileSystem fileSystem = new();

        var result = assets.Aggregate((a1, a2) => ClusterAssembliesResolver.ByHighestVersionOrLastFile(a1, a2, fileSystem));

        result.Should().BeSameAs(assets[0]);
    }

    [Fact]
    public void Returns_file_by_highest_version_in_name()
    {
        ClusterAssembliesResolver.AssetFileCandidate[] assets =
        [
            new("file.so.3", default, _package),
            new("file.so.6", default, _package),
            new("file.so.1", default, _package),
        ];
        MockFileSystem fileSystem = new();

        var result = assets.Aggregate((a1, a2) => ClusterAssembliesResolver.ByHighestVersionOrLastFile(a1, a2, fileSystem));

        result.Should().BeSameAs(assets[1]);
    }

    [Fact]
    public void Returns_file_descending()
    {
        ClusterAssembliesResolver.AssetFileCandidate[] assets =
        [
            new("file.dll", default, _package),
            new("file.so", default, _package),
            new("file.dylib", default, _package),
        ];
        MockFileSystem fileSystem = new();

        var result = assets.Aggregate((a1, a2) => ClusterAssembliesResolver.ByHighestVersionOrLastFile(a1, a2, fileSystem));

        result.Should().BeSameAs(assets[1]);
    }
}

public class ClusterAssembliesResolver_ToMainComponents
{
    [Fact]
    public void Returns_main_components()
    {
        (string, ClusterAssembliesResolver.Package)[] files =
        [
            (string.Empty, new ClusterAssembliesResolver.Package(string.Empty,
            [
                new ClusterAssembliesResolver.DependencyFile(Path.Combine("dir", "A", "Assembly.deps.json")),
                new ClusterAssembliesResolver.DependencyFile(Path.Combine("dir", "A", "False.deps.json")),
            ])),
            (string.Empty, new ClusterAssembliesResolver.Package(string.Empty,
            [
                new ClusterAssembliesResolver.DependencyFile(Path.Combine("dir", "B", "Assembly.deps.json")),
            ])),
        ];

        var components = files.ToMainComponents().ToArray();

        components.Should().SatisfyRespectively(
            e => e.PackageMainComponent.Should().Be(Path.Combine("dir", "A", "Assembly.dll")),
            e => e.PackageMainComponent.Should().Be(Path.Combine("dir", "B", "Assembly.dll")));
    }
}


public class ClusterAssembliesResolver_ResolveLatestVersion
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
