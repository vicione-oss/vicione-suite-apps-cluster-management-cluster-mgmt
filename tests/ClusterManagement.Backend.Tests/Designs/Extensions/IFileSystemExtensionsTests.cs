using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Semver;
using Xunit;
using ClusterManagement.Backend.Designs.Extensions;
using Sdk.Backend.Modules;
using NSubstitute;

namespace ClusterManagement.Backend.Tests.Designs.Extensions;

public class IFileSystemExtensionsTests
{
    public sealed class GetComponents : IFileSystemExtensionsTests
    {
        private readonly MockFileSystem _fileSystem = new();

        [Fact]
        public void Returns_path_to_assembly()
        {
            // Arrange            
            var functionBlock = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("ifm.Add", "1.2.3"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

            // Act
            var components = _fileSystem.GetComponents(_fileSystem.Directory.GetCurrentDirectory());

            // Assert
            var tempPath = _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), "ifm.Add", "1.2.3");
            components.Should().ContainSingle().Which.Value.Should().Be(tempPath);
        }

        [Fact]
        public void Reads_version_of_FunctionBlock_if_available()
        {
            // Arrange
            var functionBlock = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("ifm.Add", "1.2.3"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

            // Act
            var components = _fileSystem.GetComponents(_fileSystem.Directory.GetCurrentDirectory());

            // Assert
            components.Should().ContainSingle().Which.Key.Version.ToString().Should().Be("1.2.3");
        }

        [Fact]
        public void Throws_if_directory_structure_is_suspicious()
        {
            // Arrange
            var functionBlock = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("packages", "ifm.Add"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(functionBlock.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

            // Act
            var act = () => _fileSystem.GetComponents(_fileSystem.Directory.GetCurrentDirectory()).ToArray();

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Returns_every_available_version()
        {
            // Arrange
            var functionBlockVersionOne = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("ifm.Add", "1.0.0"));
            var functionBlockVersionTwo = _fileSystem.Directory.CreateDirectory(_fileSystem.Path.Combine("ifm.Add", "2.0.0"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(functionBlockVersionOne.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));
            _fileSystem.AddFile(_fileSystem.Path.Combine(functionBlockVersionTwo.FullName, "ifm.Add.deps.json"), new MockFileData(string.Empty));

            // Act
            var components = _fileSystem.GetComponents(_fileSystem.Directory.GetCurrentDirectory());

            // Assert
            var tempPath = _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), "ifm.Add", "1.2.3");
            components.Should().HaveCount(2).And.SatisfyRespectively(
                e => e.Key.Version.Should().Be(new SemVersion(1, 0, 0)),
                e => e.Key.Version.Should().Be(new SemVersion(2, 0, 0)));
        }
    }

    public sealed class GetDependenciesPath : IFileSystemExtensionsTests
    {
        private readonly MockFileSystem _fileSystem = new();

        [Fact]
        public void Returns_dependencies_path()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
            workspaceProvider.Cache.Returns("/tmp/cache");

            // Act
            var dependenciesPath = _fileSystem.GetDependenciesPath(workspaceProvider);

            // Assert
            dependenciesPath.Should().Be($"/tmp/cache{_fileSystem.Path.DirectorySeparatorChar}Dependencies");
        }
    }
}
