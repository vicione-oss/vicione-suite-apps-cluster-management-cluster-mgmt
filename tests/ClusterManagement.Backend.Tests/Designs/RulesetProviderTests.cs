using AngleSharp.Common;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Tests.Designs;

public class RulesetProviderTests
{
    private readonly IPackageContentProvider _packageProvider = Substitute.For<IPackageContentProvider>();
    private readonly ILogger<RulesetProvider> _logger = Substitute.For<ILogger<RulesetProvider>>();
    private readonly string _dataPortName = typeof(ViciOne.Suite.System.DataPort).FullName!;

    private RulesetProvider CreateSut() => new(_packageProvider, _logger);

    public sealed class AddSystemDataPortFunctionBlock : RulesetProviderTests
    {
        [Fact]
        public async Task Should_add_system_dataport_package_and_return_dependency()
        {
            // Arrange
            var sut = CreateSut();
            _packageProvider.TryAddPackage(
                Arg.Any<ClusterDependency>(),
                Arg.Any<IReadOnlyCollection<Type>>(),
                Arg.Any<IReadOnlyCollection<Ruleset>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var result = await sut.AddSystemDataPortFunctionBlock(TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(_dataPortName);
            await _packageProvider.Received(1).TryAddPackage(
                Arg.Is<ClusterDependency>(d => d.Name == _dataPortName),
                Arg.Any<IReadOnlyCollection<Type>>(),
                Arg.Any<IReadOnlyCollection<Ruleset>>(),
                true,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_dependency_when_package_add_fails()
        {
            // Arrange
            var sut = CreateSut();
            _packageProvider.TryAddPackage(
                Arg.Any<ClusterDependency>(),
                Arg.Any<IReadOnlyCollection<Type>>(),
                Arg.Any<IReadOnlyCollection<Ruleset>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()).Returns(false);

            // Act
            var result = await sut.AddSystemDataPortFunctionBlock(TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(_dataPortName);
        }

        [Fact]
        public async Task Should_store_dependency_for_later_retrieval()
        {
            // Arrange
            var sut = CreateSut();
            _packageProvider.TryAddPackage(
                Arg.Any<ClusterDependency>(),
                Arg.Any<IReadOnlyCollection<Type>>(),
                Arg.Any<IReadOnlyCollection<Ruleset>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var addResult = await sut.AddSystemDataPortFunctionBlock(TestContext.Current.CancellationToken);
            var getResult = sut.GetSystemDataPortDependency();

            // Assert
            getResult.Should().Be(addResult);
        }
    }

    public sealed class GetRuleset : RulesetProviderTests
    {
        [Fact]
        public void Should_return_ruleset_when_found()
        {
            // Arrange
            var sut = CreateSut();
            var expectedRuleset = RulesetFactory.CreateTestRulesetMqtt();
            var dependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };
            var rulesets = new Dictionary<string, Ruleset>
            {
                { "TestRuleset", expectedRuleset }
            };
            var packageContent = new PackageContent(
                CreateFunctionBlockDesigns(2),
                rulesets,
                false);
            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                { dependency, packageContent }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetRuleset("TestCategory", "TestRuleset");

            // Assert
            result.Should().Be(expectedRuleset);
        }

        [Fact]
        public void Should_throw_when_ruleset_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var packageContents = new Dictionary<ClusterDependency, PackageContent>();
            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                sut.GetRuleset("TestCategory", "NonExistentRuleset"));

            exception.Message.Should().Be("Can't find Ruleset for category:TestCategory and rulesetId:NonExistentRuleset");
        }

        [Fact]
        public void Should_return_first_matching_ruleset_from_multiple_packages()
        {
            // Arrange
            var sut = CreateSut();
            var ruleset1 = RulesetFactory.CreateTestRulesetMqtt();
            var ruleset2 = RulesetFactory.CreateTestRulesetMqtt();
            var dependency1 = new ClusterDependency { Name = "Package1", Version = "1.0.0" };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = "1.0.0" };

            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                {
                    dependency1,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset> { { "SharedRuleset", ruleset1 } },
                        false)
                },
                {
                    dependency2,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset> { { "SharedRuleset", ruleset2 } },
                        false)
                }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetRuleset("TestCategory", "SharedRuleset");

            // Assert
            result.Should().BeOneOf(ruleset1, ruleset2);
        }
    }

    public sealed class GetAllRulesets : RulesetProviderTests
    {
        [Fact]
        public void Should_return_all_rulesets_from_all_packages()
        {
            // Arrange
            var sut = CreateSut();
            var ruleset1 = RulesetFactory.CreateTestRulesetMqtt();
            var ruleset2 = RulesetFactory.CreateTestRulesetMqtt();
            var dependency1 = new ClusterDependency { Name = "Package1", Version = "1.0.0" };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = "1.0.0" };

            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                {
                    dependency1,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset>
                        {
                            { "Ruleset1", ruleset1 },
                        },
                        false)
                },
                {
                    dependency2,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset> { { "Ruleset2", ruleset2 } },
                        false)
                }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetAllRulesets().ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(ruleset1);
            result.Should().Contain(ruleset2);
        }

        [Fact]
        public void Should_return_empty_when_no_packages_exist()
        {
            // Arrange
            var sut = CreateSut();
            var packageContents = new Dictionary<ClusterDependency, PackageContent>();
            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetAllRulesets();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Should_return_empty_when_packages_have_no_rulesets()
        {
            // Arrange
            var sut = CreateSut();
            var dependency = new ClusterDependency { Name = "Package1", Version = "1.0.0" };
            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                {
                    dependency,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset>(),
                        false)
                }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetAllRulesets();

            // Assert
            result.Should().BeEmpty();
        }
    }

    public sealed class GetClusterDependency : RulesetProviderTests
    {
        [Fact]
        public void Should_return_dependency_when_dataport_type_found()
        {
            // Arrange
            var sut = CreateSut();
            var dependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };
            var ruleset = RulesetFactory.CreateTestRulesetMqtt();
            var packageContent = new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset> { { "TestDataPort", ruleset } },
                false);
            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                { dependency, packageContent }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetClusterDependency("TestDataPort");

            // Assert
            result.Should().Be(dependency);
        }

        [Fact]
        public void Should_throw_when_dataport_type_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var packageContents = new Dictionary<ClusterDependency, PackageContent>();
            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                sut.GetClusterDependency("NonExistentDataPort"));

            exception.Message.Should().Be("Can't find ClusterDependency for DataPort type:NonExistentDataPort");
        }

        [Fact]
        public void Should_return_first_matching_dependency_when_multiple_packages_have_same_dataport_type()
        {
            // Arrange
            var sut = CreateSut();
            var dependency1 = new ClusterDependency { Name = "Package1", Version = "1.0.0" };
            var dependency2 = new ClusterDependency { Name = "Package2", Version = "1.0.0" };
            var ruleset = RulesetFactory.CreateTestRulesetMqtt();

            var packageContents = new Dictionary<ClusterDependency, PackageContent>
            {
                {
                    dependency1,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset> { { "SharedDataPort", ruleset } },
                        false)
                },
                {
                    dependency2,
                    new PackageContent(
                        new Dictionary<Guid, FunctionBlockDesign>(),
                        new Dictionary<string, Ruleset> { { "SharedDataPort", ruleset } },
                        false)
                }
            };

            _packageProvider.GetPackageContents().Returns(packageContents);

            // Act
            var result = sut.GetClusterDependency("SharedDataPort");

            // Assert
            result.Should().BeOneOf(dependency1, dependency2);
        }
    }

    public sealed class GetSystemDataPortDependency : RulesetProviderTests
    {
        [Fact]
        public void Should_throw_when_system_dataport_not_added()
        {
            // Arrange
            var sut = CreateSut();

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                sut.GetSystemDataPortDependency());

            exception.Message.Should().Be($"Use {nameof(RulesetProvider.AddSystemDataPortFunctionBlock)} before adding rulesets");
        }

        [Fact]
        public async Task Should_return_dependency_after_adding_system_dataport()
        {
            // Arrange
            var sut = CreateSut();
            _packageProvider.TryAddPackage(
                Arg.Any<ClusterDependency>(),
                Arg.Any<IReadOnlyCollection<Type>>(),
                Arg.Any<IReadOnlyCollection<Ruleset>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var addedDependency = await sut.AddSystemDataPortFunctionBlock(TestContext.Current.CancellationToken);
            var retrievedDependency = sut.GetSystemDataPortDependency();

            // Assert
            retrievedDependency.Should().Be(addedDependency);
            retrievedDependency.Name.Should().Be(_dataPortName);
        }
    }

    private static Dictionary<Guid, FunctionBlockDesign> CreateFunctionBlockDesigns(int count)
        => Enumerable.Range(1, count).Select(_ => CreateFunctionBlockDesign())
            .Select(k => new KeyValuePair<Guid, FunctionBlockDesign>(k.Id, k))
            .ToDictionary();

    private static FunctionBlockDesign CreateFunctionBlockDesign()
    {
        var functionBlockDesign = FunctionBlockDesignBuilder.Create(Guid.NewGuid());
        functionBlockDesign.SystemInputs.Clear();
        functionBlockDesign.SystemOutputs.Clear();
        functionBlockDesign.AddOutput<double>(Guid.NewGuid(), "Value");
        return functionBlockDesign;
    }
}
