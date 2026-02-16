using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Text.Json;
using ClusterManagement.Backend.Diagnostics;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Mqtt;
using Microsoft.Extensions.Configuration;
using Sdk.SystemConfiguration;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

internal sealed class ProcessEngineHostPoolCommunication(string installDirectory, string hostsDirectory, IFileSystem fileSystem,
    IMqttObserverClient mqttObserverClient, IControlServiceManagement serviceManagement, IConfiguration configuration,
    IProcessManager? processManager = null) : IEngineHostPoolCommunication
{
    private const string PrivilegeIndicator = "-privileged";
    private const string ExecutableFileName = "executable";
    private const string ProcessIdFileName = "processid";
    private static readonly TimeSpan s_killProcessTimeout = TimeSpan.FromSeconds(15);

    private readonly string _installDirectory = fileSystem.Path.GetFullPath(installDirectory);
    private readonly string _hostsDirectory = fileSystem.Path.GetFullPath(hostsDirectory);
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IMqttObserverClient _mqttObserverClient = mqttObserverClient;
    private readonly IControlServiceManagement _serviceManagement = serviceManagement;
    private readonly IConfiguration _configuration = configuration;
    private readonly IProcessManager _processManager = processManager ?? new ProcessManager();

    public async Task<IReadOnlyDictionary<Guid, EngineHostState>> GetStates(CancellationToken cancellationToken)
    {
        Dictionary<Guid, EngineHostState> states = [];
        if (_fileSystem.Directory.Exists(_hostsDirectory))
        {
            var tasks = Task.WhenEach(
                _fileSystem.Directory.EnumerateDirectories(_hostsDirectory)
                    .Select(async hostDirectory =>
                    {
                        var id = Guid.Parse(_fileSystem.Path.GetFileName(hostDirectory));
                        var state = await GetState(id, cancellationToken);
                        return (Id: id, State: state);
                    }));

            await foreach (var task in tasks)
            {
                var (id, state) = await task;
                states.Add(id, state);
            }
        }
        return states;
    }

    public async Task<EngineHostState> GetState(Guid id, CancellationToken cancellationToken)
    {
        var workingDirectory = GetWorkingDirectory(id);
        if (!_fileSystem.Directory.Exists(workingDirectory))
            return EngineHostState.NotFound;
        var pid = await ReadProcessId(workingDirectory, cancellationToken);
        if (!pid.HasValue)
            return EngineHostState.Stopped;
        using var process = _processManager.GetProcess(pid.Value);
        if (process is null)
            return EngineHostState.Stopped;
        return process.IsRunning ? EngineHostState.Started : EngineHostState.Stopped;
    }

    private string GetWorkingDirectory(Guid id)
        => _fileSystem.Path.Combine(_hostsDirectory, id.ToString());

    private async Task<int?> ReadProcessId(string directory, CancellationToken cancellationToken)
    {
        var text = await ReadFirstLine(directory, ProcessIdFileName, cancellationToken);
        if (int.TryParse(text, CultureInfo.InvariantCulture, out var pid))
            return pid;
        return default;
    }

    private static async Task WaitForProcessExit(IProcess process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

        try
        {
            await process.WaitForExit(combinedTokenSource.Token);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            // Throws the canceled exception of the general token as usual.
            if (cancellationToken.IsCancellationRequested)
                throw;
        }
        catch (InvalidOperationException)
        {
            // There is no process associated with the process object.
        }
    }

    private static void TryKillProcess(IProcess process)
    {
        try
        {
            process.Kill();
        }
        catch
        { }
    }

    public async Task DeployHost(Guid id, CancellationToken cancellationToken)
    {
        var executablePath = GetExecutablePath();
        CheckExecutablePath(executablePath);
        var workingDirectory = GetWorkingDirectory(id);
        EnsureDirectory(workingDirectory);
        await WriteExecutablePath(workingDirectory, executablePath, cancellationToken);
    }

    private void CheckExecutablePath(string executablePath)
    {
        if (!_fileSystem.File.Exists(executablePath))
            throw new InvalidOperationException($"A engine host cannot be deployed without an existing executable file.");
    }

    private string GetExecutablePath()
    {
        var executable = _fileSystem.Path.Combine(_installDirectory, "ViciOne.ManagedEngine.Host");
        if (OperatingSystem.IsWindows() && !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            executable += ".exe";
        return executable;
    }

    private Task WriteExecutablePath(string workingDirectory, string executablePath, CancellationToken cancellationToken)
        => WriteFile(workingDirectory, ExecutableFileName, executablePath, cancellationToken);

    private void EnsureDirectory(string workingDirectory) => _fileSystem.Directory.CreateDirectory(workingDirectory);

    public async Task TearDownHost(Guid id, CancellationToken cancellationToken)
    {
        var workingDirectory = GetWorkingDirectory(id);
        if (!UseProcess())
            await EnsureAccessiblePermissions(workingDirectory, id, cancellationToken);
        RemoveDirectory(workingDirectory);
    }

    private void RemoveDirectory(string workingDirectory)
    {
        if (_fileSystem.Directory.Exists(workingDirectory))
            _fileSystem.Directory.Delete(workingDirectory, true);
    }

    public async Task StartHost(Guid id, bool elevatedPrivileges, CancellationToken cancellationToken)
    {
        if (await GetState(id, cancellationToken) == EngineHostState.Stopped)
        {
            var workingDirectory = GetWorkingDirectory(id);
            if (UseProcess())
            {
                var startInfo = await GetProcessStartInfo(workingDirectory, cancellationToken);
                await ObserveStart(id, async ct =>
                {
                    var processId = StartProcess(startInfo);
                    await WriteProcessId(workingDirectory, processId, ct);
                }, cancellationToken);
            }
            else
            {
                await EnsureAccessiblePermissions(workingDirectory, id, cancellationToken);
                await ObserveStart(id, ct => StartService(id, elevatedPrivileges, ct), cancellationToken);
            }
        }
    }

    private bool UseProcess() =>
        OperatingSystem.IsWindows() ||
        _configuration.GetSection("HostManagement")?.GetSection("MockClient")?.GetValue<bool?>("Enabled", false) == true;

    private async Task ObserveStart(Guid id, Func<CancellationToken, Task> startMethod, CancellationToken cancellationToken)
    {
        var response = await _mqttObserverClient.Observe(startMethod, string.Concat(id, "/state"), cancellationToken);
        if (!string.Equals(response.Payload, "upandrunning", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Engine host '{id}' did not start with reason '{response.Payload}'.");
    }

    private async Task<ProcessStartInfo> GetProcessStartInfo(string workingDirectory, CancellationToken cancellationToken)
    {
        var executablePath = await ReadExecutablePath(workingDirectory, cancellationToken);
        return new()
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
        };
    }

    private async Task<string> ReadExecutablePath(string workingDirectory, CancellationToken cancellationToken)
        => await ReadFirstLine(workingDirectory, ExecutableFileName, cancellationToken)
            ?? throw new InvalidOperationException($"Executable path of engine host is not configured.");

    private int StartProcess(ProcessStartInfo startInfo)
    {
        using var process = _processManager.StartProcess(startInfo);
        return process.Id;
    }

    private Task WriteProcessId(string directory, int processId, CancellationToken cancellationToken)
        => WriteFile(directory, ProcessIdFileName, processId.ToString(CultureInfo.InvariantCulture), cancellationToken);

    private async Task EnsureAccessiblePermissions(string workingDirectory, Guid id, CancellationToken cancellationToken)
    {
        var filePath = GetConfigurationFilePath(workingDirectory);
        if (_fileSystem.File.Exists(filePath))
        {
            try
            {
                await using var stream = _fileSystem.FileStream.New(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (UnauthorizedAccessException)
            {
                await _serviceManagement.TryControlService(ServiceCommand.Start, $"enginehost-reset@{id}.service", cancellationToken);
            }
        }
    }

    private async Task StartService(Guid id, bool elevatedPrivileges, CancellationToken cancellationToken)
    {
        var privileged = elevatedPrivileges ? PrivilegeIndicator : string.Empty;
        await _serviceManagement.TryControlService(ServiceCommand.Start, $"enginehost{privileged}@{id}.service", cancellationToken);
    }

    private async Task WriteFile(string directory, string fileName, string content, CancellationToken cancellationToken)
    {
        var filePath = _fileSystem.Path.Combine(directory, fileName);
        await _fileSystem.File.WriteAllTextAsync(filePath, content, cancellationToken);
    }

    private async Task<string?> ReadFirstLine(string directory, string fileName, CancellationToken cancellationToken)
    {
        var filePath = _fileSystem.Path.Combine(directory, fileName);
        if (_fileSystem.File.Exists(filePath))
        {
            await foreach (var line in _fileSystem.File.ReadLinesAsync(filePath, cancellationToken))
                return line;
        }
        return default;
    }

    public async Task StopHost(Guid id, CancellationToken cancellationToken)
    {
        if (await GetState(id, cancellationToken) == EngineHostState.Started)
        {
            var workingDirectory = GetWorkingDirectory(id);
            var useProcess = UseProcess();

            if (useProcess)
                await SendTerminate(workingDirectory);
            else
                await StopService(id, cancellationToken);

            await EnsureTermination(workingDirectory, useProcess ? s_killProcessTimeout : s_killProcessTimeout * 2, cancellationToken);
            if (useProcess)
                DeleteProcessId(workingDirectory);
        }
    }

    private async Task SendTerminate(string workingDirectory)
        => await _fileSystem.File.Create(_fileSystem.Path.Combine(workingDirectory, "terminate")).DisposeAsync();

    private async Task StopService(Guid id, CancellationToken cancellationToken)
    {
        var service = await FindService($"enginehost*@{id}.service", cancellationToken);
        if (service is not null)
        {
            if (service.Contains(PrivilegeIndicator, StringComparison.OrdinalIgnoreCase))
                await _serviceManagement.TryControlService(ServiceCommand.Stop, $"enginehost{PrivilegeIndicator}@{id}.service", cancellationToken);
            else
                await _serviceManagement.TryControlService(ServiceCommand.Stop, $"enginehost@{id}.service", cancellationToken);
        }
    }

    private async Task<string?> FindService(string pattern, CancellationToken cancellationToken)
    {
        var output = await Bash($"systemctl --type service --all --full --no-pager --plain list-units \"{pattern}\"", cancellationToken);
        return output.Split(Environment.NewLine)
            .Skip(1) // skip header line
            .TakeWhile(line => !string.IsNullOrWhiteSpace(line)) // take until the end of list
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(0) ?? string.Empty) // try to extract service name
            .Select(service => service.Trim())
            .FirstOrDefault(service => !string.IsNullOrWhiteSpace(service)); // filter out empty service names. this also works for empty output and failures.
    }

    private async Task<string> Bash(string cmd, CancellationToken cancellationToken)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase);
        using var process = _processManager.StartProcess(new()
        {
            FileName = "bash",
            Arguments = $"-c \"{escapedArgs}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        });

        using CancellationTokenSource timeoutTokenSource = new(TimeSpan.FromSeconds(2));
        using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

        IProcessResult result;
        try
        {
            result = await process.WaitForExit(combinedTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            if (process.IsRunning)
                process.Kill();
            throw;
        }

        if (result.ExitCode == 0)
        {
            return await result.GetStandardOutput(cancellationToken);
        }
        else
        {
            var error = await result.GetStandardError(cancellationToken);
            throw new InvalidOperationException($"Command failed with exit code {result.ExitCode} = {error}");
        }
    }

    private async Task EnsureTermination(string workingDirectory, TimeSpan killTimeout, CancellationToken cancellationToken)
    {
        var processId = await ReadProcessId(workingDirectory, cancellationToken);
        if (processId.HasValue)
        {
            using var process = _processManager.GetProcess(processId.Value);
            if (process is not null)
            {
                await WaitForProcessExit(process, killTimeout, cancellationToken);
                if (process.IsRunning)
                    TryKillProcess(process);
            }
        }
    }

    private void DeleteProcessId(string directory)
    {
        var filePath = _fileSystem.Path.Combine(directory, ProcessIdFileName);
        _fileSystem.File.Delete(filePath);
    }

    public async Task<int> CompareHostAppSettings(Guid id, EngineHostAppSettings appSettings, CancellationToken cancellationToken)
    {
        var workingDirectory = GetWorkingDirectory(id);
        var filePath = GetConfigurationFilePath(workingDirectory);
        return await CompareConfiguration(appSettings, filePath, cancellationToken);
    }

    private async Task<int> CompareConfiguration(EngineHostAppSettings appSettings, string filePath, CancellationToken cancellationToken)
    {
        var existingConfiguration = string.Empty;
        if (_fileSystem.File.Exists(filePath))
            existingConfiguration = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
        var configuration = JsonSerializer.Serialize(appSettings, JsonSetup.DefaultOptions);
        return string.Compare(configuration, existingConfiguration, StringComparison.Ordinal);
    }

    public async Task SetHostAppSettings(Guid id, EngineHostAppSettings appSettings, CancellationToken cancellationToken)
    {
        var workingDirectory = GetWorkingDirectory(id);
        var filePath = GetConfigurationFilePath(workingDirectory);
        await WriteConfiguration(filePath, appSettings, cancellationToken);
    }

    private string GetConfigurationFilePath(string workingDirectory) => _fileSystem.Path.Combine(workingDirectory, "appsettings.json");

    private async Task WriteConfiguration(string filePath, object appSettings, CancellationToken cancellationToken)
    {
        await using var stream = _fileSystem.FileStream.New(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, appSettings, JsonSetup.DefaultOptions, cancellationToken);
    }

    public Task<SemVersion?> GetEngineHostVersion(CancellationToken cancellationToken, Guid? id = default)
        => GetVersion("engine host", ["ViciOne.ManagedEngine.Host.dll",], cancellationToken, id);

    public Task<SemVersion?> GetManagedEngineVersion(CancellationToken cancellationToken, Guid? engineHostId = default)
        => GetVersion("managed engine", ["ViciOne.ManagedEngine.dll",], cancellationToken, engineHostId);

    private async Task<SemVersion?> GetVersion(string componentName, IEnumerable<string> assemblyFileNames, CancellationToken cancellationToken,
        Guid? id = default)
    {
        var installDirectory = await GetInstallDirectory(id, cancellationToken);
        var assemblyFileName = FindFirstFiles(installDirectory, assemblyFileNames)
            ?? throw new InvalidOperationException($"No suitable file could be found to determine the version of the {componentName}.");
        var version = ReadAssemblyVersion(assemblyFileName)
            ?? throw new InvalidOperationException($"Could not read {componentName} version using assembly '{assemblyFileName}'.");
        if (!SemVersion.TryParse(version, SemVersionStyles.Any, out var semVersion))
            throw new InvalidOperationException($"Could not parse {componentName} version '{version}' to a valid semantic version.");
        return semVersion;
    }

    private async Task<string> GetInstallDirectory(Guid? id, CancellationToken cancellationToken)
    {
        var installDirectory = _installDirectory;
        if (id.HasValue)
        {
            var workingDirectory = GetWorkingDirectory(id.Value);
            var executablePath = await ReadExecutablePath(workingDirectory, cancellationToken);
            installDirectory = _fileSystem.Path.GetDirectoryName(executablePath)!;
        }
        return installDirectory;
    }

    private string? FindFirstFiles(string directory, IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var filePath = _fileSystem.Path.Combine(directory, fileName);
            if (_fileSystem.File.Exists(filePath))
                return filePath;
        }
        return default;
    }

    private static string? ReadAssemblyVersion(string filePath)
    {
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
        return fileVersionInfo.ProductVersion;
    }
}
