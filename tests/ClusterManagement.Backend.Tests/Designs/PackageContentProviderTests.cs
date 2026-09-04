using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Tests.Designs;

[Collection(NonParallelCollectionDefinitionClass.NonParallelCollection)]
public class PackageContentProviderTests
{
    private readonly FileSystem _fileSystem = new();
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IServiceProvider _services;

    private readonly ClusterDependency _constantDependency = PackageFactory.ConstantPackageDependency;
    private readonly ClusterDependency _logicDependency = PackageFactory.LogicPackageDependency;
    private readonly Type _systemDataPortType = typeof(ViciOne.Suite.System.DataPort);

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
            .AddSingleton(Substitute.For<ILogger<PackageContentProvider>>())
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
            var result = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);

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
                IFileSystemExtensions.DependenciesDirectory,
                _constantDependency.Name,
                _constantDependency.Version.ToString());

            SetupClusterManagementAssemblyContext();

            _dependencyStore.LoadDependencies(TestContext.Current.CancellationToken)
                .Returns([_constantDependency]);

            // Act
            var result = await provider.TryAddPackage(_constantDependency, fbPath, false, TestContext.Current.CancellationToken);

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
            var dependency = new ClusterDependency { Name = "NonExistentPackage", Version = "1.0.0" };

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
            var dependency = new ClusterDependency { Name = "MissingPackage", Version = "3.2.1" };

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

            await provider.TryAddPackage(_constantDependency, [], [], false, TestContext.Current.CancellationToken);
            await provider.TryAddPackage(_logicDependency, [], [], false, TestContext.Current.CancellationToken);

            // Act
            var result = provider.GetPackageContents();

            // Assert
            result.Should().HaveCount(2);
            result.Keys.Should().Contain(_constantDependency);
            result.Keys.Should().Contain(_logicDependency);
        }

        [Fact]
        public async Task Should_return_packages_with_error_on_failure()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var failureDependency = new ClusterDependency { Name = "ViciOne.Failure", Version = "0.2.0" };

            SetupClusterManagementAssemblyContext();

            await provider.TryAddPackage(failureDependency, [], [], false, TestContext.Current.CancellationToken);

            // Act
            var result = provider.GetPackageContents();

            // Assert
            result.Should().HaveCount(1);
            result.Keys.Should().Contain(failureDependency);
            result.Values.Should().Contain(p => p.Error != null);
        }
    }

    public sealed class TryAddPackageFromDirectory : PackageContentProviderTests
    {
        [Fact]
        public async Task Should_return_false_when_package_already_exists()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };

            await provider.TryAddPackage(dependency, [], [], false, TestContext.Current.CancellationToken);

            // Act
            var result = await provider.TryAddPackage(dependency, "TODO", false, TestContext.Current.CancellationToken);

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
            var result = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);

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
            var result = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], true, TestContext.Current.CancellationToken);

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

            await provider.TryAddPackage(_constantDependency, [], [], false, TestContext.Current.CancellationToken);

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Should_add_package_with_empty_types_and_rulesets()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [], [], false, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse();
            var contents = provider.GetPackageContents();
            contents[_constantDependency].Designs.Should().BeEmpty();
            contents[_constantDependency].Rulesets.Should().BeEmpty();
            contents[_constantDependency].Error.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_add_package_with_hide_flag_false()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            // Act
            var result = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);

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
            var result = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], true, TestContext.Current.CancellationToken);

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
            var result = await provider.TryAddPackage(_constantDependency, [], rulesets, false, TestContext.Current.CancellationToken);

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
            var result1 = await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);
            var result2 = await provider.TryAddPackage(_logicDependency, [], [], false, TestContext.Current.CancellationToken);

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
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
                Task.Run(() => provider.TryAddPackage(dep, [_systemDataPortType], [], false, TestContext.Current.CancellationToken))
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
                .Select(_ => Task.Run(() => provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken)))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            var successCount = tasks.Count(t => t.Result);
            successCount.Should().Be(1);

            var contents = provider.GetPackageContents();
            contents.Should().HaveCount(1);
        }
    }

    public sealed class AddFailedPackageTests : PackageContentProviderTests
    {
        [Fact]
        public void Should_add_failed_package_with_error()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "ViciOne.Broken", Version = "1.0.0" };
            var error = new ErrorInfo(0, "Download failed");

            // Act
            provider.AddFailedPackage(dependency, error);

            // Assert
            var content = provider.GetPackageContent(dependency);
            content.Error.Should().BeSameAs(error);
            content.Designs.Should().BeEmpty();
            content.Rulesets.Should().BeEmpty();
            content.Hide.Should().BeTrue();
        }

        [Fact]
        public void Should_not_overwrite_existing_package_when_adding_failed()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "ViciOne.Broken", Version = "1.0.0" };
            var firstError = new ErrorInfo(0, "First error");
            var secondError = new ErrorInfo(0, "Second error");

            provider.AddFailedPackage(dependency, firstError);

            // Act
            provider.AddFailedPackage(dependency, secondError);

            // Assert
            var content = provider.GetPackageContent(dependency);
            content.Error.Should().BeSameAs(firstError, "TryAdd should not overwrite existing entry");
        }

        [Fact]
        public void Should_be_retrievable_via_GetPackageContents()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "ViciOne.Missing", Version = "2.0.0" };
            var error = new ErrorInfo(0, "Not found on disk");

            // Act
            provider.AddFailedPackage(dependency, error);

            // Assert
            var contents = provider.GetPackageContents();
            contents.Should().ContainKey(dependency);
            contents[dependency].Error.Should().BeSameAs(error);
        }

        [Fact]
        public async Task Should_not_overwrite_successfully_loaded_package()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();

            SetupClusterManagementAssemblyContext();

            await provider.TryAddPackage(_constantDependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);

            // Act
            provider.AddFailedPackage(_constantDependency, new ErrorInfo(0, "Late failure"));

            // Assert
            var content = provider.GetPackageContent(_constantDependency);
            content.Error.Should().BeNull("the successful package should not be replaced");
            content.Designs.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_prevent_TryAddPackage_from_overwriting_failed_package()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependency = new ClusterDependency { Name = "ViciOne.FailedFirst", Version = "1.0.0" };
            var error = new ErrorInfo(0, "Download failed");

            SetupClusterManagementAssemblyContext();

            provider.AddFailedPackage(dependency, error);

            // Act
            var result = await provider.TryAddPackage(dependency, [_systemDataPortType], [], false, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeFalse("TryAddPackage should not overwrite a failed package entry");
            var content = provider.GetPackageContent(dependency);
            content.Error.Should().BeSameAs(error);
        }

        [Fact]
        public void Should_handle_concurrent_AddFailedPackage_calls()
        {
            // Arrange
            var provider = _services.GetRequiredService<PackageContentProvider>();
            var dependencies = Enumerable.Range(0, 20)
                .Select(i => new ClusterDependency { Name = $"ViciOne.Pkg{i}", Version = "1.0.0" })
                .ToList();

            // Act
            Parallel.ForEach(dependencies, dep =>
                provider.AddFailedPackage(dep, new ErrorInfo(0, $"Failed: {dep.Name}")));

            // Assert
            var contents = provider.GetPackageContents();
            contents.Should().HaveCount(20);
            contents.Values.Should().OnlyContain(c => c.Error != null);
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
