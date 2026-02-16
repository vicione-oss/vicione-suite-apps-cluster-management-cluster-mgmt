using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using ClusterManagement.Backend.EngineHosts;

namespace ClusterManagement.Backend.Tests.EngineHosts;

public class EngineHostPool_Migrate
{
    private readonly MockFileSystem _fileSystem;
    private readonly EngineHostWorkingDirectory _engineHostWorkingDirectory;
    private readonly Guid _engineHostId = Guid.NewGuid();
    private readonly EngineHostPool _engineHostPool;

    public EngineHostPool_Migrate()
    {
        var directory = Directory.GetCurrentDirectory();
        _fileSystem = new(null, directory);
        _engineHostWorkingDirectory = new(_fileSystem, _engineHostId);
        _engineHostPool = new(directory, directory, _fileSystem);
    }

    [Fact]
    public async Task Does_nothing_if_hosts_directory_does_not_exist()
    {
        EngineHostPool engineHostPool = new(_fileSystem.Directory.GetCurrentDirectory(),
            Path.Combine(nameof(EngineHostPool_Migrate), nameof(Does_nothing_if_hosts_directory_does_not_exist)),
            _fileSystem);

        await engineHostPool.Migrate(TestContext.Current.CancellationToken);

        _fileSystem.Directory.Exists(_engineHostWorkingDirectory.Path).Should().BeFalse();
    }

    [Fact]
    public async Task Does_nothing_if_no_working_directories_exist()
    {
        await _engineHostPool.Migrate(TestContext.Current.CancellationToken);

        _fileSystem.Directory.Exists(_engineHostWorkingDirectory.Path).Should().BeFalse();
    }

    [Fact]
    public async Task Does_nothing_if_executable_file_exists()
    {
        _engineHostWorkingDirectory.CreateDirectory();
        _engineHostWorkingDirectory.WriteExecutablePath("content");

        await _engineHostPool.Migrate(TestContext.Current.CancellationToken);

        _fileSystem.GetFile(_engineHostWorkingDirectory.ExecutableFile).TextContents.Should().Be("content");
    }

    [Fact]
    public async Task Create_executable_file_if_missing()
    {
        _engineHostWorkingDirectory.CreateDirectory();

        await _engineHostPool.Migrate(TestContext.Current.CancellationToken);

        _fileSystem.GetFile(_engineHostWorkingDirectory.ExecutableFile).TextContents.Should().Be(
            _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(
                $"0.27.0-{RuntimeInformation.RuntimeIdentifier}",
                string.Concat("ViciOne.ManagedEngine.Host", OperatingSystem.IsWindows() ? ".exe" : string.Empty))));
    }
}
