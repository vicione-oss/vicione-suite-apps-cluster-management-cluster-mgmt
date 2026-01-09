using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Tests.EngineHosts;

internal sealed class EngineHostWorkingDirectory
{
    private readonly MockFileSystem _fileSystem;

    internal string Path { get; }
    internal string ExecutableFile { get; }
    internal string ProcessIdFile { get; }
    internal string AppSettingsFile { get; }
    internal string TerminateFile { get; }

    internal EngineHostWorkingDirectory(MockFileSystem fileSystem, Guid hostId)
    {
        _fileSystem = fileSystem;
        Path = _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), hostId.ToString());
        ExecutableFile = _fileSystem.Path.Combine(Path, "executable");
        ProcessIdFile = _fileSystem.Path.Combine(Path, "processid");
        AppSettingsFile = _fileSystem.Path.Combine(Path, "appsettings.json");
        TerminateFile = _fileSystem.Path.Combine(Path, "terminate");
    }

    internal void CreateDirectory() => _fileSystem.AddDirectory(Path);
    internal void WriteExecutablePath(string executablePath) => _fileSystem.AddFile(ExecutableFile, executablePath);
    internal void WriteProcessId(int id) => _fileSystem.AddFile(ProcessIdFile, id.ToString(CultureInfo.InvariantCulture));
    internal int ReadProcessId() => int.Parse(_fileSystem.GetFile(ProcessIdFile).TextContents, CultureInfo.InvariantCulture);

    internal void WriteAppSettings(EngineHostAppSettings configuration)
    {
        var json = JsonSerializer.Serialize(configuration, JsonSetup.DefaultOptions);
        _fileSystem.AddFile(AppSettingsFile, new MockFileData(json));
    }

    internal EngineHostAppSettings ReadAppSettings()
    {
        var json = _fileSystem.GetFile(AppSettingsFile).TextContents;
        return JsonSerializer.Deserialize<EngineHostAppSettings>(json, JsonSetup.DefaultOptions) ?? throw new InvalidOperationException("Could not read app settings.");
    }
}
