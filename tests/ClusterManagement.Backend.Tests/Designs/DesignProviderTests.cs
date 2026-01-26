using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Public.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.TreeBuilder.Rules;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class DesignProviderTests
{
    private readonly IPackageContentProvider _packageContentProvider = Substitute.For<IPackageContentProvider>();
    private readonly ILogger<DesignProvider> _logger = Substitute.For<ILogger<DesignProvider>>();
    private readonly DesignProvider _sut;

    public DesignProviderTests()
    {
        _sut = new DesignProvider(_packageContentProvider, _logger);
    }

    public sealed class CreateResolver : DesignProviderTests
    {
        [Fact]
        public async Task Should_return_dependency_resolver()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var design = CreateFunctionBlockDesign(Guid.NewGuid(), "TestDesign");

            var packageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent> { { dependency, packageContent } });

            // Act
            var result = await _sut.CreateResolver();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<ClusterDependencyResolver>();
        }

        [Fact]
        public async Task Should_return_resolver_with_all_packages()
        {
            // Arrange
            var dependency1 = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) };

            var design1 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design1");
            var design2 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design2");

            var packageContent1 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design1.Id, design1 } },
                new Dictionary<string, Ruleset>(),
                false);

            var packageContent2 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design2.Id, design2 } },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency1, packageContent1 },
                    { dependency2, packageContent2 }
                });

            // Act
            var result = await _sut.CreateResolver();

            // Assert
            result.Should().NotBeNull();
        }
    }

    public sealed class GetClusterDependencies : DesignProviderTests
    {
        [Fact]
        public async Task Should_return_all_dependencies_when_includeDataPortDependencies_is_true()
        {
            // Arrange
            var dependency1 = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) };
            var dataPortDependency = new ClusterDependency { Name = "DataPort", Version = new Version(1, 0, 0) };

            var design = CreateFunctionBlockDesign(Guid.NewGuid(), "Design");
            var ruleset = RulesetFactory.CreateTestRulesetMqtt();

            var packageContent1 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            var packageContent2 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            var dataPortPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset> { { "ruleset1", ruleset } },
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency1, packageContent1 },
                    { dependency2, packageContent2 },
                    { dataPortDependency, dataPortPackageContent }
                });

            // Act
            var result = await _sut.GetClusterDependencies(includeDataPortDependencies: true);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(dependency1);
            result.Should().Contain(dependency2);
            result.Should().Contain(dataPortDependency);
        }

        [Fact]
        public async Task Should_exclude_dataport_dependencies_when_includeDataPortDependencies_is_false()
        {
            // Arrange
            var dependency1 = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) };
            var dataPortDependency = new ClusterDependency { Name = "DataPort", Version = new Version(1, 0, 0) };

            var design = CreateFunctionBlockDesign(Guid.NewGuid(), "Design");
            var ruleset = RulesetFactory.CreateTestRulesetMqtt();

            var packageContent1 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            var packageContent2 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            var dataPortPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset> { { "ruleset1", ruleset } },
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency1, packageContent1 },
                    { dependency2, packageContent2 },
                    { dataPortDependency, dataPortPackageContent }
                });

            // Act
            var result = await _sut.GetClusterDependencies(includeDataPortDependencies: false);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(dependency1);
            result.Should().Contain(dependency2);
            result.Should().NotContain(dataPortDependency);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_no_packages_exist()
        {
            // Arrange
            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>());

            // Act
            var result = await _sut.GetClusterDependencies(includeDataPortDependencies: false);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_use_default_value_for_includeDataPortDependencies_parameter()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dataPortDependency = new ClusterDependency { Name = "DataPort", Version = new Version(1, 0, 0) };

            var design = CreateFunctionBlockDesign(Guid.NewGuid(), "Design");
            var ruleset = RulesetFactory.CreateTestRulesetMqtt();

            var packageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                false);

            var dataPortPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset> { { "ruleset1", ruleset } },
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency, packageContent },
                    { dataPortDependency, dataPortPackageContent }
                });

            // Act
            var result = await _sut.GetClusterDependencies();

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(dependency);
        }
    }

    public sealed class GetFunctionBlockDesignIds : DesignProviderTests
    {
        [Fact]
        public async Task Should_return_design_ids_for_single_dependency()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var designId1 = Guid.NewGuid();
            var designId2 = Guid.NewGuid();

            var design1 = CreateFunctionBlockDesign(designId1, "Design1");
            var design2 = CreateFunctionBlockDesign(designId2, "Design2");

            var packageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>
                {
                    { design1.Id, design1 },
                    { design2.Id, design2 }
                },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent> { { dependency, packageContent } });

            // Act
            var result = await _sut.GetFunctionBlockDesignIds([dependency]);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(designId1);
            result.Should().Contain(designId2);
        }

        [Fact]
        public async Task Should_return_design_ids_for_multiple_dependencies()
        {
            // Arrange
            var dependency1 = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) };

            var designId1 = Guid.NewGuid();
            var designId2 = Guid.NewGuid();
            var designId3 = Guid.NewGuid();

            var design1 = CreateFunctionBlockDesign(designId1, "Design1");
            var design2 = CreateFunctionBlockDesign(designId2, "Design2");
            var design3 = CreateFunctionBlockDesign(designId3, "Design3");

            var packageContent1 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>
                {
                    { design1.Id, design1 },
                    { design2.Id, design2 }
                },
                new Dictionary<string, Ruleset>(),
                false);

            var packageContent2 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design3.Id, design3 } },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency1, packageContent1 },
                    { dependency2, packageContent2 }
                });

            // Act
            var result = await _sut.GetFunctionBlockDesignIds([dependency1, dependency2]);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(designId1);
            result.Should().Contain(designId2);
            result.Should().Contain(designId3);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_no_dependencies_provided()
        {
            // Arrange
            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>());

            // Act
            var result = await _sut.GetFunctionBlockDesignIds([]);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_exclude_hidden_packages()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "HiddenPackage", Version = new Version(1, 0, 0) };
            var designId = Guid.NewGuid();
            var design = CreateFunctionBlockDesign(designId, "HiddenDesign");

            var hiddenPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                true);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent> { { dependency, hiddenPackageContent } });

            // Act
            var result = await _sut.GetFunctionBlockDesignIds([dependency]);

            // Assert
            result.Should().BeEmpty();
        }
    }

    public sealed class GetFunctionBlockDesigns : DesignProviderTests
    {
        [Fact]
        public async Task Should_return_designs_for_single_dependency()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var design1 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design1");
            var design2 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design2");

            var packageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>
                {
                    { design1.Id, design1 },
                    { design2.Id, design2 }
                },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent> { { dependency, packageContent } });

            // Act
            var result = await _sut.GetFunctionBlockDesigns([dependency]);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(design1);
            result.Should().Contain(design2);
        }

        [Fact]
        public async Task Should_return_designs_for_multiple_dependencies()
        {
            // Arrange
            var dependency1 = new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) };

            var design1 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design1");
            var design2 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design2");
            var design3 = CreateFunctionBlockDesign(Guid.NewGuid(), "Design3");

            var packageContent1 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>
                {
                    { design1.Id, design1 },
                    { design2.Id, design2 }
                },
                new Dictionary<string, Ruleset>(),
                false);

            var packageContent2 = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design3.Id, design3 } },
                new Dictionary<string, Ruleset>(),
                false);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { dependency1, packageContent1 },
                    { dependency2, packageContent2 }
                });

            // Act
            var result = await _sut.GetFunctionBlockDesigns([dependency1, dependency2]);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(design1);
            result.Should().Contain(design2);
            result.Should().Contain(design3);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_no_dependencies_provided()
        {
            // Arrange
            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>());

            // Act
            var result = await _sut.GetFunctionBlockDesigns([]);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_exclude_designs_from_hidden_packages()
        {
            // Arrange
            var dependency = new ClusterDependency { Name = "HiddenPackage", Version = new Version(1, 0, 0) };
            var design = CreateFunctionBlockDesign(Guid.NewGuid(), "HiddenDesign");

            var hiddenPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { design.Id, design } },
                new Dictionary<string, Ruleset>(),
                true);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent> { { dependency, hiddenPackageContent } });

            // Act
            var result = await _sut.GetFunctionBlockDesigns([dependency]);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_handle_mixed_visible_and_hidden_packages()
        {
            // Arrange
            var visibleDependency = new ClusterDependency { Name = "VisiblePackage", Version = new Version(1, 0, 0) };
            var hiddenDependency = new ClusterDependency { Name = "HiddenPackage", Version = new Version(2, 0, 0) };

            var visibleDesign = CreateFunctionBlockDesign(Guid.NewGuid(), "VisibleDesign");
            var hiddenDesign = CreateFunctionBlockDesign(Guid.NewGuid(), "HiddenDesign");

            var visiblePackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { visibleDesign.Id, visibleDesign } },
                new Dictionary<string, Ruleset>(),
                false);

            var hiddenPackageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign> { { hiddenDesign.Id, hiddenDesign } },
                new Dictionary<string, Ruleset>(),
                true);

            _packageContentProvider.GetPackageContents()
                .Returns(new Dictionary<ClusterDependency, PackageContent>
                {
                    { visibleDependency, visiblePackageContent },
                    { hiddenDependency, hiddenPackageContent }
                });

            // Act
            var result = await _sut.GetFunctionBlockDesigns([visibleDependency, hiddenDependency]);

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(visibleDesign);
            result.Should().NotContain(hiddenDesign);
        }
    }

    private static FunctionBlockDesign CreateFunctionBlockDesign(Guid designId, string name)
    {
        var functionBlockDesign = FunctionBlockDesignBuilder.Create(designId);
        functionBlockDesign.Name = name;
        functionBlockDesign.SystemInputs.Clear();
        functionBlockDesign.SystemOutputs.Clear();
        functionBlockDesign.AddOutput<double>(Guid.NewGuid(), "Value");
        return functionBlockDesign;
    }
}
