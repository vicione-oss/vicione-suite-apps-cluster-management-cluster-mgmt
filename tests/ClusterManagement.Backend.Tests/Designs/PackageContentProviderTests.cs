using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageContentProviderTests
{
    private readonly FileSystem _fileSystem = new();
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IServiceProvider _services;

    private readonly ClusterDependency _constantDependency = new() { Name = "ViciOne.Constant", Version = new Version(0, 34, 0) };
    private readonly ClusterDependency _logicDependency = new() { Name = "ViciOne.Logic", Version = new Version(0, 34, 0) };

    public PackageContentProviderTests()
    {
        var local = Assembly.GetExecutingAssembly().Location;
        var folder = Path.GetDirectoryName(local) ?? string.Empty;

        _workspaceProvider.Cache.Returns(folder);
        _services = new ServiceCollection()
            .AddSingleton<PackageContentProvider>()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_dependencyStore)
            .AddSingleton(_workspaceProvider)
            .BuildServiceProvider();
    }

    public sealed class GetPackageContent : PackageContentProviderTests
    {
        [Fact]
        public async Task Should_return_package_content_for_existing_dependency()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false);

            // Assert
            result.Should().BeTrue();
            var packageContent = provider.GetPackageContent(_constantDependency);
            packageContent.Should().NotBeNull();
            packageContent.Designs.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_return_package_content_with_designs()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var fbPath = _fileSystem.Path.Combine(_workspaceProvider.Cache,
                "Dependencies",
                _constantDependency.Name,
                _constantDependency.Version.ToString());

            SetupClusterManagementAssemblyContext();

            _dependencyStore.LoadDependencies(default)
                .Returns([_constantDependency]);

            // Act
            var result = await provider.TryAddPackage(_constantDependency, fbPath, false);

            // Assert
            result.Should().BeTrue();
            var packageContent = provider.GetPackageContent(_constantDependency);
            packageContent.Should().NotBeNull();
            packageContent.Designs.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_throw_key_not_found_exception_when_dependency_not_exists()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "NonExistentPackage", Version = new Version(1, 0, 0) };

            SetupClusterManagementAssemblyContext();

            // Act
            var act = () => provider.GetPackageContent(dependency);

            // Assert
            act.Should().Throw<KeyNotFoundException>()
                .WithMessage($"Package content for dependency '{dependency}' not found.");
        }

        [Fact]
        public void Should_throw_exception_with_correct_message_format()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "MissingPackage", Version = new Version(3, 2, 1) };

            SetupClusterManagementAssemblyContext();

            // Act
            var act = () => provider.GetPackageContent(dependency);

            // Assert
            act.Should().Throw<KeyNotFoundException>();
        }
    }

    public sealed class GetPackageContents : PackageContentProviderTests
    {
        [Fact]
        public void Should_return_empty_dictionary_when_no_packages_added()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = provider.GetPackageContents();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_all_added_packages()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            await provider.TryAddPackage(_constantDependency, [], [], false);
            await provider.TryAddPackage(_logicDependency, [], [], false);

            // Act
            var result = provider.GetPackageContents();

            // Assert
            result.Should().HaveCount(2);
            result.Keys.Should().Contain(_constantDependency);
            result.Keys.Should().Contain(_logicDependency);
        }
    }

    public sealed class TryAddPackageFromDirectory : PackageContentProviderTests
    {
        [Fact]
        public async Task Should_return_false_when_package_already_exists()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) };

            await provider.TryAddPackage(dependency, [], [], false);

            // Act
            var result = await provider.TryAddPackage(dependency, "TODO", false);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_add_package_with_hide_flag_false_by_default()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Hide.Should().BeFalse();
        }

        [Fact]
        public async Task Should_add_package_with_hide_flag_true()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], true);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Hide.Should().BeTrue();
        }
    }

    public sealed class TryAddPackageFromTypes : PackageContentProviderTests
    {
        [Fact]
        public async Task Should_return_false_when_package_already_exists()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            await provider.TryAddPackage(_constantDependency, [], [], false);

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_add_package_with_empty_types_and_rulesets()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Designs.Should().BeEmpty();
            contents[_constantDependency].Rulesets.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_add_package_with_hide_flag_false()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Hide.Should().BeFalse();
        }

        [Fact]
        public async Task Should_add_package_with_hide_flag_true()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], true);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Hide.Should().BeTrue();
        }

        [Fact]
        public async Task Should_filter_rulesets_without_root()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var rulesetWithRoot = RulesetFactory.CreateTestRulesetMqtt();
            var rulesetWithoutRoot = RulesetFactory.CreateInvalidRuleset();
            var rulesets = new List<Ruleset> { rulesetWithRoot, rulesetWithoutRoot };

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], rulesets, false);

            // Assert
            result.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Rulesets.Should().HaveCount(1);
            contents[_constantDependency].Rulesets.Should().ContainKey(rulesetWithRoot.Root!.Id);
        }

        [Fact]
        public async Task Should_add_multiple_packages_from_types()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result1 = await provider.TryAddPackage(_constantDependency, [], [], false);
            var result2 = await provider.TryAddPackage(_logicDependency, [], [], false);

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            var contents = provider.GetPackageContents();
            contents.Should().HaveCount(2);
        }
    }

    public sealed class ThreadSafety : PackageContentProviderTests
    {
        [Fact]
        public async Task Should_handle_concurrent_package_additions()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependencies = new[] { _constantDependency, _logicDependency };

            SetupClusterManagementAssemblyContext();

            // Act
            var tasks = dependencies.Select(dep =>
                Task.Run(() => provider.TryAddPackage(dep, [], [], false))
            ).ToArray();

            await Task.WhenAll(tasks);

            // Assert
            var contents = provider.GetPackageContents();
            contents.Should().HaveCount(2);
            tasks.All(t => t.Result).Should().BeTrue();
        }

        [Fact]
        public async Task Should_prevent_duplicate_additions_in_concurrent_scenario()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => provider.TryAddPackage(_constantDependency, [], [], false)))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            var successCount = tasks.Count(t => t.Result);
            successCount.Should().Be(1);

            var contents = provider.GetPackageContents();
            contents.Should().HaveCount(1);
        }
    }

    private static void SetupClusterManagementAssemblyContext()
    {
        var name = typeof(ClusterManagementBackendModule).Assembly.GetName().Name;
        if (AssemblyLoadContext.All.Any(ctx => ctx.Name == name))
            return;

        var context = new AssemblyLoadContext(name, true);
        context.LoadFromAssemblyName(typeof(ClusterManagementBackendModule).Assembly.GetName());
    }
}
