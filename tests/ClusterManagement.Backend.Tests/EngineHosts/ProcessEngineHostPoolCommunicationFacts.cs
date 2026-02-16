using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using AwesomeAssertions.Extensions;
using ClusterManagement.Backend.Diagnostics;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Backend.Tests.TestUtils;
using Microsoft.Extensions.Configuration;
using NSubstitute.ExceptionExtensions;
using Sdk.SystemConfiguration;
using Sdk.Testing;
using Semver;

namespace ClusterManagement.Backend.Tests.EngineHosts;

public class ProcessEngineHostPoolCommunicationFacts
{
    public class GetStates
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Returns_Started_if_process_is_running()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            _context.ProcessManager.GetProcess(123).Returns(process);

            var actual = await _context.Communication.GetStates(CancellationToken.None);

            var item = actual.Should().ContainSingle().Subject;
            item.Key.Should().Be(_context.HostId);
            item.Value.Should().Be(EngineHostState.Started);
        }

        [Fact]
        public async Task Returns_Stopped_if_process_id_is_known_but_no_process_could_be_found()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteProcessId(123456);

            var actual = await _context.Communication.GetStates(CancellationToken.None);

            var item = actual.Should().ContainSingle().Subject;
            item.Key.Should().Be(_context.HostId);
            item.Value.Should().Be(EngineHostState.Stopped);
        }

        [Fact]
        public async Task Returns_Stopped_if_process_id_is_unknown()
        {
            _context.WorkingDirectory.CreateDirectory();

            var actual = await _context.Communication.GetStates(CancellationToken.None);

            var item = actual.Should().ContainSingle().Subject;
            item.Key.Should().Be(_context.HostId);
            item.Value.Should().Be(EngineHostState.Stopped);
        }

        [Fact]
        public async Task Returns_no_item_if_no_working_directory_exists()
        {
            var actual = await _context.Communication.GetStates(CancellationToken.None);

            actual.Should().BeEmpty();
        }

        [Fact]
        public async Task Returns_no_item_if_hosts_directory_does_not_exist()
        {
            EngineHostContext context = new(
                hostsDirectory: Path.Combine(nameof(ProcessEngineHostPoolCommunicationFacts), nameof(GetStates), nameof(Returns_no_item_if_hosts_directory_does_not_exist)));

            var actual = await context.Communication.GetStates(CancellationToken.None);

            actual.Should().BeEmpty();
        }
    }

    public class GetState
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Returns_Started_if_process_is_running()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            _context.ProcessManager.GetProcess(123).Returns(process);

            var actual = await _context.Communication.GetState(_context.HostId, CancellationToken.None);

            actual.Should().Be(EngineHostState.Started);
        }

        [Fact]
        public async Task Returns_Stopped_if_process_id_is_known_but_no_process_could_be_found()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteProcessId(123456);

            var actual = await _context.Communication.GetState(_context.HostId, CancellationToken.None);

            actual.Should().Be(EngineHostState.Stopped);
        }

        [Fact]
        public async Task Returns_Stopped_if_process_id_is_unknown()
        {
            _context.WorkingDirectory.CreateDirectory();

            var actual = await _context.Communication.GetState(_context.HostId, CancellationToken.None);

            actual.Should().Be(EngineHostState.Stopped);
        }

        [Fact]
        public async Task Returns_NotFound_if_working_directory_does_not_exist()
        {
            var actual = await _context.Communication.GetState(_context.HostId, CancellationToken.None);

            actual.Should().Be(EngineHostState.NotFound);
        }
    }

    public class DeployHost
    {
        private readonly EngineHostContext _context = new();

        public DeployHost()
        {
            var executable = _context.FileSystem.Path.Combine(_context.FileSystem.Directory.GetCurrentDirectory(), "ViciOne.ManagedEngine.Host");
            if (OperatingSystem.IsWindows() && !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                executable += ".exe";
            _context.FileSystem.AddFile(executable, new MockFileData(string.Empty));
        }

        [Fact]
        public async Task Does_not_clean_the_directory_if_host_is_already_deployed()
        {
            _context.WorkingDirectory.CreateDirectory();
            var executable = _context.FileSystem.Path.Combine(_context.WorkingDirectory.Path, "bin", "executable");
            _context.FileSystem.AddFile(executable, new MockFileData("bytes"));

            await _context.Communication.DeployHost(_context.HostId, CancellationToken.None);

            var content = await _context.FileSystem.File.ReadAllTextAsync(executable, TestContext.Current.CancellationToken);
            content.Should().Be("bytes");
        }

        [Fact]
        public async Task Creates_working_directory_with_executable_path_for_host()
        {
            await _context.Communication.DeployHost(_context.HostId, CancellationToken.None);

            _context.FileSystem.Directory.Exists(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeTrue(); // base directory for all engine host working directories
            _context.FileSystem.Directory.GetDirectories(_context.FileSystem.Directory.GetCurrentDirectory()).Should().ContainSingle(); // working directories for engine hosts
            _context.FileSystem.Directory.Exists(_context.WorkingDirectory.Path).Should().BeTrue(); // working directory for engine host
            _context.FileSystem.FileExists(_context.WorkingDirectory.ExecutableFile).Should().BeTrue(); // configuration file for executable path in working directory
        }

        [Fact]
        public async Task Throws_if_executable_does_not_exist()
        {
            EngineHostContext context = new(installDirectory: Path.GetTempPath());

            var act = context.Communication.Awaiting(x => x.DeployHost(_context.HostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*deploy*without*executable*");

            _context.FileSystem.Directory.GetDirectories(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeEmpty(); // working directories for engine hosts
            _context.FileSystem.Directory.Exists(_context.WorkingDirectory.Path).Should().BeFalse(); // working directory for engine host
            _context.FileSystem.FileExists(_context.WorkingDirectory.ExecutableFile).Should().BeFalse(); // configuration file for executable path in working directory
        }
    }

    public class TearDownHost
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Does_nothing_if_working_directory_is_already_deleted()
        {
            await _context.Communication.TearDownHost(_context.HostId, CancellationToken.None);

            _context.FileSystem.Directory.Exists(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeTrue();
            _context.FileSystem.Directory.GetDirectories(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeEmpty();
        }

        [Fact(Skip = "LinuxOnly", SkipWhen = nameof(PlatformSkip.IfNotLinux), SkipType = typeof(PlatformSkip))]
        public async Task Resets_engine_host_permissions_if_file_access_is_unauthorized()
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteAppSettings(new());
            context.FileSystem.AddDirectory(context.FileSystem.Path.Combine(context.WorkingDirectory.Path, "resources"));
            var fileStream = Substitute.For<IFileStreamFactory>();
            fileStream.New(Arg.Any<string>(), Arg.Any<FileMode>(), Arg.Any<FileAccess>(), Arg.Any<FileShare>()).Throws<UnauthorizedAccessException>();
            context.FileSystem.FileStream.Returns(fileStream);

            await context.Communication.TearDownHost(context.HostId, CancellationToken.None);

            await context.ServiceManagement.Received(1).TryControlService(
                Arg.Is(ServiceCommand.Start),
                Arg.Is($"enginehost-reset@{context.HostId}.service"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Deletes_working_directory_for_host()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteAppSettings(new());
            _context.FileSystem.AddDirectory(_context.FileSystem.Path.Combine(_context.WorkingDirectory.Path, "resources"));

            await _context.Communication.TearDownHost(_context.HostId, CancellationToken.None);

            _context.FileSystem.Directory.Exists(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeTrue();
            _context.FileSystem.Directory.GetDirectories(_context.FileSystem.Directory.GetCurrentDirectory()).Should().BeEmpty();
        }
    }

    public sealed class StartHost
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Does_nothing_if_directory_is_missing()
        {
            await _context.Communication.StartHost(_context.HostId, false, CancellationToken.None);

            _context.ProcessManager.ReceivedCalls().Should().BeEmpty();
            _context.ServiceManagement.ReceivedCalls().Should().BeEmpty();
            _context.MqttObserverClient.ReceivedCalls().Should().BeEmpty();
        }

        [Fact]
        public async Task Does_nothing_if_process_is_running()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            _context.ProcessManager.GetProcess(Arg.Is(123)).Returns(process);

            await _context.Communication.StartHost(_context.HostId, false, CancellationToken.None);

            _context.WorkingDirectory.ReadProcessId().Should().Be(123);
            _context.MqttObserverClient.ReceivedCalls().Should().BeEmpty();
        }

        [Fact(Skip = "WindowsOnly", SkipWhen = nameof(PlatformSkip.IfNotWindows), SkipType = typeof(PlatformSkip))]
        public async Task Throws_for_unconfigured_executable_path()
        {
            EngineHostContext context = new();
            context.WorkingDirectory.CreateDirectory();

            var act = context.Communication.Awaiting(_ => _.StartHost(context.HostId, false, CancellationToken.None));

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"*executable*path*not*configured*");
        }

        [Fact(Skip = "WindowsOnly", SkipWhen = nameof(PlatformSkip.IfNotWindows), SkipType = typeof(PlatformSkip))]
        public async Task Throws_for_empty_configured_executable_path()
        {
            EngineHostContext context = new(osProcessManager: true);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteExecutablePath("   ");

            var act = context.Communication.Awaiting(_ => _.StartHost(context.HostId, false, CancellationToken.None));

            await act.Should().ThrowExactlyAsync<Win32Exception>().WithMessage($"*try*start*'   '*");
        }

        [Fact(Skip = "WindowsOnly", SkipWhen = nameof(PlatformSkip.IfNotWindows), SkipType = typeof(PlatformSkip))]
        public async Task Throws_for_missing_executable()
        {
            EngineHostContext context = new(osProcessManager: true);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteExecutablePath("ViciOne.ManagedEngine.Host.exe");

            var act = context.Communication.Awaiting(_ => _.StartHost(context.HostId, false, CancellationToken.None));

            await act.Should().ThrowExactlyAsync<Win32Exception>().WithMessage($"*try*start*'ViciOne.ManagedEngine.Host.exe'*");
        }

        [Theory(Skip = "WindowsOnly", SkipWhen = nameof(PlatformSkip.IfNotWindows), SkipType = typeof(PlatformSkip))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Starts_the_executable_on_windows(bool hostManagementAvailable)
        {
            EngineHostContext context = new(hostManagementAvailable: hostManagementAvailable);
            var process = Substitute.For<IProcess>();
            process.Id.Returns(123);
            context.ProcessManager.StartProcess(Arg.Any<ProcessStartInfo>()).Returns(process);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteExecutablePath("ViciOne.ManagedEngine.Host");

            await context.Communication.StartHost(context.HostId, false, CancellationToken.None);

            context.WorkingDirectory.Invoking(_ => _.ReadProcessId()).Should().NotThrow().Which.Should().Be(123);
            context.ProcessManager.Received(1).StartProcess(FluentArg.Assert<ProcessStartInfo>(info =>
            {
                info.WorkingDirectory.Should().Be(context.WorkingDirectory.Path);
                info.FileName.Should().Be("ViciOne.ManagedEngine.Host");
                info.UseShellExecute.Should().BeTrue();
            })!);
            context.ServiceManagement.ReceivedCalls().Should().BeEmpty();
        }

        [Fact]
        public async Task Starts_the_executable_if_host_management_is_not_available()
        {
            EngineHostContext context = new(hostManagementAvailable: false);
            var process = Substitute.For<IProcess>();
            process.Id.Returns(123);
            context.ProcessManager.StartProcess(Arg.Any<ProcessStartInfo>()).Returns(process);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteExecutablePath("ViciOne.ManagedEngine.Host");

            await context.Communication.StartHost(context.HostId, false, CancellationToken.None);

            context.WorkingDirectory.Invoking(_ => _.ReadProcessId()).Should().NotThrow().Which.Should().Be(123);
            context.ProcessManager.Received(1).StartProcess(FluentArg.Assert<ProcessStartInfo>(info =>
            {
                info.WorkingDirectory.Should().Be(context.WorkingDirectory.Path);
                info.FileName.Should().Be("ViciOne.ManagedEngine.Host");
                info.UseShellExecute.Should().BeTrue();
            })!);
            context.ServiceManagement.ReceivedCalls().Should().BeEmpty();
        }

        [Fact(Skip = "LinuxOnly", SkipWhen = nameof(PlatformSkip.IfNotLinux), SkipType = typeof(PlatformSkip))]
        public async Task Resets_engine_host_permissions_if_file_access_is_unauthorized()
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            var fileStream = Substitute.For<IFileStreamFactory>();
            fileStream.New(Arg.Any<string>(), Arg.Any<FileMode>(), Arg.Any<FileAccess>(), Arg.Any<FileShare>()).Throws<UnauthorizedAccessException>();
            context.FileSystem.FileStream.Returns(fileStream);
            context.WorkingDirectory.CreateDirectory();
            context.WorkingDirectory.WriteAppSettings(new EngineHostAppSettings());

            await context.Communication.StartHost(context.HostId, false, CancellationToken.None);

            await context.ServiceManagement.Received(1).TryControlService(
                Arg.Is(ServiceCommand.Start),
                Arg.Is($"enginehost-reset@{context.HostId}.service"),
                Arg.Any<CancellationToken>());
        }

        [Theory(Skip = "LinuxOnly", SkipWhen = nameof(PlatformSkip.IfNotLinux), SkipType = typeof(PlatformSkip))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Starts_the_service_if_host_management_is_available(bool elevatedPrivileges)
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            context.WorkingDirectory.CreateDirectory();

            await context.Communication.StartHost(context.HostId, elevatedPrivileges, CancellationToken.None);

            context.FileSystem.FileExists(context.WorkingDirectory.ProcessIdFile).Should().BeFalse();
            context.ProcessManager.DidNotReceive().StartProcess(Arg.Any<ProcessStartInfo>());
            await context.ServiceManagement.Received(1).TryControlService(
                Arg.Is(ServiceCommand.Start),
                Arg.Is($"enginehost{(elevatedPrivileges ? "-privileged" : string.Empty)}@{context.HostId}.service"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Waits_for_up_and_running_host()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteExecutablePath("ViciOne.ManagedEngine.Host");
            var startupTime = 2.Seconds();
            _context.MqttObserverClient.Observe(
                Arg.Do<Func<CancellationToken, Task>>(func =>
                {
                    Task.Delay(startupTime).GetAwaiter().GetResult();
                    func(default).GetAwaiter().GetResult();
                }), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new MqttObserverResponse() { Payload = "upandrunning", });
            using CancellationTokenSource cancellationTokenSource = new();

            var act = _context.Communication.Awaiting(_ => _.StartHost(_context.HostId, false, cancellationTokenSource.Token));

            var watch = Stopwatch.StartNew();
            await act.Should().CompleteWithinAsync(startupTime.Multiply(2));
            watch.Stop();
            watch.Elapsed.Should().BeGreaterThan(startupTime.Subtract(50.Milliseconds()));
            await _context.MqttObserverClient.Received(1).Observe(Arg.Any<Func<CancellationToken, Task>>(), Arg.Is($"{_context.HostId}/state"),
                Arg.Is(cancellationTokenSource.Token));
        }

        [Fact]
        public async Task Throws_for_not_up_and_running_message_from_host()
        {
            _context.WorkingDirectory.CreateDirectory();
            _context.WorkingDirectory.WriteExecutablePath("ViciOne.ManagedEngine.Host");
            _context.MqttObserverClient.Observe(Arg.Do<Func<CancellationToken, Task>>(async func => await func(default)),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new MqttObserverResponse() { Payload = "otherReason", });
            using CancellationTokenSource cancellationTokenSource = new();

            var act = _context.Communication.Awaiting(_ => _.StartHost(_context.HostId, false, cancellationTokenSource.Token));

            await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"*engine host*not*start*reason*otherReason*");
            await _context.MqttObserverClient.Received(1).Observe(Arg.Any<Func<CancellationToken, Task>>(), Arg.Is($"{_context.HostId}/state"),
                Arg.Is(cancellationTokenSource.Token));
        }
    }

    public sealed class StopHost
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Does_nothing_if_process_is_not_running()
        {
            await _context.Communication.StopHost(_context.HostId, CancellationToken.None);

            _context.FileSystem.FileExists(_context.WorkingDirectory.TerminateFile).Should().BeFalse();
        }

        [Fact(Skip = "WindowsOnly", SkipWhen = nameof(PlatformSkip.IfNotWindows), SkipType = typeof(PlatformSkip))]
        public async Task Creates_terminate_file_if_process_is_running_on_windows()
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            context.ProcessManager.GetProcess(Arg.Is(123)).Returns(process);

            await context.Communication.StopHost(context.HostId, CancellationToken.None);

            context.FileSystem.FileExists(context.WorkingDirectory.TerminateFile).Should().BeTrue();
            context.FileSystem.FileExists(context.WorkingDirectory.ProcessIdFile).Should().BeFalse();
            await process.Received(1).WaitForExit(Arg.Any<CancellationToken>());
            context.ServiceManagement.ReceivedCalls().Should().BeEmpty();
        }

        [Fact]
        public async Task Creates_terminate_file_if_process_is_running_and_host_management_is_not_available()
        {
            EngineHostContext context = new(hostManagementAvailable: false);
            context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            context.ProcessManager.GetProcess(Arg.Is(123)).Returns(process);

            await context.Communication.StopHost(context.HostId, CancellationToken.None);

            context.FileSystem.FileExists(context.WorkingDirectory.TerminateFile).Should().BeTrue();
            context.FileSystem.FileExists(context.WorkingDirectory.ProcessIdFile).Should().BeFalse();
            await process.Received(1).WaitForExit(Arg.Any<CancellationToken>());
            context.ServiceManagement.ReceivedCalls().Should().BeEmpty();
        }

        [Theory(Skip = "LinuxOnly", SkipWhen = nameof(PlatformSkip.IfNotLinux), SkipType = typeof(PlatformSkip))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Stops_the_service_if_host_management_is_available(bool elevatedPrivileges)
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            context.WorkingDirectory.WriteProcessId(123);
            var serviceName = $"enginehost{(elevatedPrivileges ? "-privileged" : string.Empty)}@{context.HostId}.service";
            var engineHost = Substitute.For<IProcess>();
            engineHost.IsRunning.Returns(true);
            context.ProcessManager.GetProcess(Arg.Is(123)).Returns(engineHost);
            var bash = Substitute.For<IProcess>();
            var bashResult = Substitute.For<IProcessResult>();
            bashResult.ExitCode.Returns(0);
            bashResult.GetStandardOutput(Arg.Any<CancellationToken>()).Returns($"""
            UNIT                                     LOAD   ACTIVE SUB     DESCRIPTION
            {serviceName}                            loaded active exited  

            Legend: LOAD   → Reflects whether the unit definition was properly loaded.
                    ACTIVE → The high-level unit activation state, i.e. generalization of SUB.
                    SUB    → The low-level unit activation state, values depend on unit type.

            32 loaded units listed.
            """);
            bash.WaitForExit(Arg.Any<CancellationToken>()).Returns(bashResult);
            context.ProcessManager.StartProcess(default!).ReturnsForAnyArgs(bash);

            await context.Communication.StopHost(context.HostId, CancellationToken.None);

            context.FileSystem.FileExists(context.WorkingDirectory.TerminateFile).Should().BeFalse();
            context.FileSystem.FileExists(context.WorkingDirectory.ProcessIdFile).Should().BeTrue();
            await engineHost.Received(1).WaitForExit(Arg.Any<CancellationToken>());
            await context.ServiceManagement.Received(1).TryControlService(Arg.Is(ServiceCommand.Stop), Arg.Is(serviceName), Arg.Any<CancellationToken>());
        }

        [Fact(Skip = "LinuxOnly", SkipWhen = nameof(PlatformSkip.IfNotLinux), SkipType = typeof(PlatformSkip))]
        public async Task Does_not_stop_the_service_if_the_service_is_missing()
        {
            EngineHostContext context = new(hostManagementAvailable: true);
            context.WorkingDirectory.WriteProcessId(123);
            var engineHost = Substitute.For<IProcess>();
            engineHost.IsRunning.Returns(true);
            context.ProcessManager.GetProcess(Arg.Is(123)).Returns(engineHost);
            var bash = Substitute.For<IProcess>();
            var bashResult = Substitute.For<IProcessResult>();
            bashResult.ExitCode.Returns(0);
            bashResult.GetStandardOutput(Arg.Any<CancellationToken>()).Returns("""
            UNIT                                     LOAD   ACTIVE SUB     DESCRIPTION

            0 loaded units listed.
            """);
            bash.WaitForExit(Arg.Any<CancellationToken>()).Returns(bashResult);
            context.ProcessManager.StartProcess(default!).ReturnsForAnyArgs(bash);

            await context.Communication.StopHost(context.HostId, CancellationToken.None);

            context.FileSystem.FileExists(context.WorkingDirectory.TerminateFile).Should().BeFalse();
            context.FileSystem.FileExists(context.WorkingDirectory.ProcessIdFile).Should().BeTrue();
            await engineHost.Received(1).WaitForExit(Arg.Any<CancellationToken>());
            await context.ServiceManagement.DidNotReceiveWithAnyArgs().TryControlService(default, default!, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Waits_for_the_process_to_terminate()
        {
            var stopTime = 2.Seconds();
            _context.WorkingDirectory.WriteProcessId(123);
            var process = Substitute.For<IProcess>();
            process.IsRunning.Returns(true);
            process.WhenForAnyArgs(x => x.WaitForExit(default))
                .Do(_ => Task.Delay(stopTime).GetAwaiter().GetResult());
            _context.ProcessManager.GetProcess(Arg.Is(123)).Returns(process);

            var act = _context.Communication.Awaiting(_ => _.StopHost(_context.HostId, CancellationToken.None));

            var watch = Stopwatch.StartNew();
            await act.Should().CompleteWithinAsync(stopTime.Multiply(2));
            watch.Stop();
            watch.Elapsed.Should().BeGreaterThan(stopTime.Subtract(50.Milliseconds()));
            await process.Received(1).WaitForExit(Arg.Any<CancellationToken>());
        }
    }

    public class SetHostAppSettingsAsync
    {
        private readonly EngineHostContext _context = new();

        [Fact]
        public async Task Writes_app_settings()
        {
            _context.WorkingDirectory.CreateDirectory();
            EngineHostAppSettings appSettings = new() { Id = Guid.NewGuid(), };

            await _context.Communication.SetHostAppSettings(_context.HostId, appSettings, CancellationToken.None);

            _context.WorkingDirectory.ReadAppSettings().Should().BeEquivalentTo(appSettings);
        }

        [Fact]
        public async Task Overwrites_existing_app_settings()
        {
            _context.WorkingDirectory.WriteAppSettings(new());
            EngineHostAppSettings appSettings = new() { Id = Guid.NewGuid(), };

            await _context.Communication.SetHostAppSettings(_context.HostId, appSettings, CancellationToken.None);

            _context.WorkingDirectory.ReadAppSettings().Should().BeEquivalentTo(appSettings);
        }

        [Fact]
        public async Task Throws_IOException_if_app_settings_is_not_writeable()
        {
            _context.WorkingDirectory.WriteAppSettings(new());
            _context.FileSystem.GetFile(_context.WorkingDirectory.AppSettingsFile).AllowedFileShare = FileShare.None;

            var act = _context.Communication.Awaiting(_ => _.SetHostAppSettings(_context.HostId, new(), CancellationToken.None));

            await act.Should().ThrowAsync<IOException>();
        }
    }

    public sealed class GetEngineHostVersion : IDisposable
    {
        private const string LatestAssemblyFileName = "ViciOne.ManagedEngine.Host.dll";
        private static readonly IReadOnlyCollection<string> s_assemblyFileNames = ["ViciOne.ManagedEngine.Host.dll",];

        private readonly TemporaryDirectory _directory;
        private readonly ProcessEngineHostPoolCommunication _communication;

        public GetEngineHostVersion()
        {
            _directory = new();
            _communication = new(_directory.Path, ".", new FileSystem(),
                Substitute.For<IMqttObserverClient>(), Substitute.For<IControlServiceManagement>(), Substitute.For<IConfiguration>());
        }

        public void Dispose() => _directory.Dispose();

        [Fact]
        public async Task Throws_if_no_file_is_existing()
        {
            var act = _communication.Invoking(_ => _.GetEngineHostVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*no*file*found*to*version*engine host*");
        }

        [Fact]
        public async Task Throws_if_file_is_not_an_assembly()
        {
            _directory.CreateFile(LatestAssemblyFileName);

            var act = _communication.Invoking(_ => _.GetEngineHostVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*read*engine host*version*{LatestAssemblyFileName}*");
        }

        [Fact]
        public async Task Throws_if_assembly_has_no_version()
        {
            var assemblyFilePath = _directory.CreateFile(LatestAssemblyFileName);
            CreateAssembly(assemblyFilePath);

            var act = _communication.Invoking(_ => _.GetEngineHostVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*read*engine host*version*{LatestAssemblyFileName}*");
        }

        [Fact]
        public async Task Throws_if_assembly_has_no_parsable_semantic_version()
        {
            var assemblyFilePath = _directory.CreateFile(LatestAssemblyFileName);
            File.Copy(Assembly.GetExecutingAssembly().Location, assemblyFilePath, true);

            var act = _communication.Invoking(_ => _.GetEngineHostVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*parse*engine host*version*1.2.3.4*semantic*");
        }

        [Theory]
        [MemberData(nameof(AssemblyList))]
        public async Task Returns_semantic_version_in_specified_file_order(IReadOnlyCollection<string> assemblyFileNames)
        {
            var expectedAssembly = Assembly.GetCallingAssembly().Location;
            var expectedVersion = FileVersionInfo.GetVersionInfo(expectedAssembly).ProductVersion;
            var index = 0;
            foreach (var assemblyFileName in assemblyFileNames)
            {
                var assemblyFilePath = _directory.CreateFile(assemblyFileName);
                if (index++ == 0)
                    File.Copy(expectedAssembly, assemblyFilePath, true);
            }

            var actual = await _communication.GetEngineHostVersion(CancellationToken.None);

            actual.Should().BeEquivalentTo(SemVersion.Parse(expectedVersion ?? string.Empty, SemVersionStyles.Any),
                o => o.ComparingByMembers<SemVersion>());
        }

        public static TheoryData<IReadOnlyCollection<string>> AssemblyList()
        {
            TheoryData<IReadOnlyCollection<string>> data = [];
            for (var i = 0; i < s_assemblyFileNames.Count; i++)
                data.Add([.. s_assemblyFileNames.Skip(i)]);
            return data;
        }
    }

    public sealed class GetManagedEngineVersion : IDisposable
    {
        private const string LatestAssemblyFileName = "ViciOne.ManagedEngine.dll";
        private static readonly IReadOnlyCollection<string> s_assemblyFileNames = ["ViciOne.ManagedEngine.dll",];

        private readonly TemporaryDirectory _directory;
        private readonly ProcessEngineHostPoolCommunication _communication;

        public GetManagedEngineVersion()
        {
            _directory = new();
            _communication = new(_directory.Path, ".", new FileSystem(),
                Substitute.For<IMqttObserverClient>(), Substitute.For<IControlServiceManagement>(), Substitute.For<IConfiguration>());
        }

        public void Dispose() => _directory.Dispose();

        [Fact]
        public async Task Throws_if_no_file_is_existing()
        {
            var act = _communication.Invoking(_ => _.GetManagedEngineVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*no*file*found*to*version*managed engine*");
        }

        [Fact]
        public async Task Throws_if_file_is_not_an_assembly()
        {
            _directory.CreateFile(LatestAssemblyFileName);

            var act = _communication.Invoking(_ => _.GetManagedEngineVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*read*managed engine*version*{LatestAssemblyFileName}*");
        }

        [Fact]
        public async Task Throws_if_assembly_has_no_version()
        {
            var assemblyFilePath = _directory.CreateFile(LatestAssemblyFileName);
            CreateAssembly(assemblyFilePath);

            var act = _communication.Invoking(_ => _.GetManagedEngineVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*read*managed engine*version*{LatestAssemblyFileName}*");
        }

        [Fact]
        public async Task Throws_if_assembly_has_no_parsable_semantic_version()
        {
            var assemblyFilePath = _directory.CreateFile(LatestAssemblyFileName);
            File.Copy(Assembly.GetExecutingAssembly().Location, assemblyFilePath, true);

            var act = _communication.Invoking(_ => _.GetManagedEngineVersion(CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*not*parse*managed engine*version*1.2.3.4*semantic*");
        }

        [Theory]
        [MemberData(nameof(AssemblyList))]
        public async Task Returns_semantic_version_in_specified_file_order(IReadOnlyCollection<string> assemblyFileNames)
        {
            var expectedAssembly = Assembly.GetCallingAssembly().Location;
            var expectedVersion = FileVersionInfo.GetVersionInfo(expectedAssembly).ProductVersion;
            var index = 0;
            foreach (var assemblyFileName in assemblyFileNames)
            {
                var assemblyFilePath = _directory.CreateFile(assemblyFileName);
                if (index++ == 0)
                    File.Copy(expectedAssembly, assemblyFilePath, true);
            }

            var actual = await _communication.GetManagedEngineVersion(CancellationToken.None);

            actual.Should().BeEquivalentTo(SemVersion.Parse(expectedVersion ?? string.Empty, SemVersionStyles.Any),
                o => o.ComparingByMembers<SemVersion>());
        }

        public static TheoryData<IReadOnlyCollection<string>> AssemblyList()
        {
            TheoryData<IReadOnlyCollection<string>> data = [];
            for (var i = 0; i < s_assemblyFileNames.Count; i++)
                data.Add([.. s_assemblyFileNames.Skip(i)]);
            return data;
        }
    }

    private static void CreateAssembly(string path)
    {
        MetadataBuilder metadata = new();

        metadata.AddModule(
            0,
            metadata.GetOrAddString(Path.GetFileNameWithoutExtension(path)),
            metadata.GetOrAddGuid(new Guid()),
            default,
            default);

        BlobBuilder peBlob = new();
        ManagedPEBuilder peBuilder = new(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), peBlob);
        peBuilder.Serialize(peBlob);
        using var stream = File.OpenWrite(path);
        peBlob.WriteContentTo(stream);
    }
}

internal sealed class EngineHostContext
{
    internal EngineHostWorkingDirectory WorkingDirectory { get; }
    internal MockFileSystem FileSystem { get; }
    internal IMqttObserverClient MqttObserverClient { get; }
    internal IControlServiceManagement ServiceManagement { get; }
    internal ProcessEngineHostPoolCommunication Communication { get; }
    internal IConfiguration Configuration { get; }
    internal IProcessManager ProcessManager { get; }
    internal Guid HostId { get; } = Guid.NewGuid();

    internal EngineHostContext(string? installDirectory = null, string? hostsDirectory = null, bool hostManagementAvailable = false, bool osProcessManager = false)
    {
        FileSystem = Substitute.ForPartsOf<MockFileSystem>(null, Directory.GetCurrentDirectory());
        WorkingDirectory = new(FileSystem, HostId);
        MqttObserverClient = Substitute.For<IMqttObserverClient>();
        MqttObserverClient.Observe(Arg.Do<Func<CancellationToken, Task>>(func => func(default).GetAwaiter().GetResult()),
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MqttObserverResponse() { Payload = "upandrunning", });
        ServiceManagement = Substitute.For<IControlServiceManagement>();
        Configuration = new ConfigurationBuilder()
           .AddInMemoryCollection(
            [
               new("HostManagement:MockClient:Enabled", (!hostManagementAvailable).ToString(CultureInfo.InvariantCulture)),
            ])
           .Build();
        ProcessManager = osProcessManager ? new ProcessManager() : Substitute.For<IProcessManager>();
        hostsDirectory ??= FileSystem.Directory.GetCurrentDirectory();
        installDirectory ??= FileSystem.Directory.GetCurrentDirectory();
        Communication = new(installDirectory, hostsDirectory, FileSystem,
            MqttObserverClient, ServiceManagement, Configuration, ProcessManager);
    }
}
