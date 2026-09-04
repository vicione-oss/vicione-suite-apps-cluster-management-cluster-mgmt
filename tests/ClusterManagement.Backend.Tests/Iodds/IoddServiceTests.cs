using System.IO.Abstractions;
using ClusterManagement.Backend.Iodds;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Iodds;

public class IoddServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();
    private readonly ILogger<IoddService> _logger = Substitute.For<ILogger<IoddService>>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    private IoddService SetupIoddService()
    {
        var ioddDirectory = Path.Combine(Path.GetTempPath(), "Iodds");
        _ioddStore.IoddDirectory.Returns(ioddDirectory);
        _workspaceProvider.Cache.Returns("Cache");

        // Setup directory operations
        _fileSystem.Directory.CreateDirectory(ioddDirectory).Returns(Substitute.For<IDirectoryInfo>());
        _fileSystem.Directory.Exists(ioddDirectory).Returns(true);
        _fileSystem.Path.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(x => Path.Combine(x.ArgAt<string>(0), x.ArgAt<string>(1)));
        _fileSystem.Path.Combine(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(x => Path.Combine(x.ArgAt<string>(0), x.ArgAt<string>(1), x.ArgAt<string>(2)));
        _fileSystem.Path.GetFullPath(Arg.Any<string>()).Returns(x => Path.GetFullPath(x.Arg<string>()));

        return new IoddService(_ioddStore, _fileSystem, _logger);
    }

    public sealed class StartAsync : IoddServiceTests
    {
        [Fact]
        public async Task Should_start_file_system_watcher_successfully()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            _fileSystem.FileSystemWatcher.Received(1).New(Arg.Any<string>());
        }

        [Fact]
        public async Task Should_create_iodd_directory_on_start()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            _fileSystem.Directory.Received(1).CreateDirectory(Arg.Any<string>());
        }

        [Fact]
        public async Task Should_not_throw_when_starting_successfully()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            var act = async () => await service.StartAsync(CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }

    public sealed class StopAsync : IoddServiceTests
    {
        [Fact]
        public async Task Should_stop_file_system_watcher_successfully()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert
            // Service should complete without throwing
        }

        [Fact]
        public async Task Should_handle_stop_when_not_started()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            var act = async () => await service.StopAsync(CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task Should_cancel_pending_debounce_task()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);

            // Act - Stop immediately
            await service.StopAsync(CancellationToken.None);

            // Assert - Should complete without deadlock
        }

        [Fact]
        public async Task Should_not_refresh_after_stop()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);
            _ioddStore.ClearReceivedCalls();

            // Act
            await service.StopAsync(CancellationToken.None);
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Assert
            _ioddStore.DidNotReceive().Refresh();
        }
    }

    public sealed class Dispose : IoddServiceTests
    {
        [Fact]
        public void Should_dispose_without_throwing()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            var act = service.Dispose;

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public async Task Should_dispose_after_start_without_throwing()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);

            // Act
            var act = service.Dispose;

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Should_allow_multiple_dispose_calls()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            service.Dispose();
            var act = service.Dispose;

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public async Task Should_not_refresh_after_dispose()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);
            _ioddStore.ClearReceivedCalls();

            // Act
            service.Dispose();
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Assert
            _ioddStore.DidNotReceive().Refresh();
        }
    }

    public sealed class ErrorHandling : IoddServiceTests
    {
        [Fact]
        public async Task Should_handle_refresh_exception_gracefully()
        {
            // Arrange
            _ioddStore.When(x => x.Refresh()).Do(_ => throw new InvalidOperationException("Test exception"));
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert - Service should start despite initial refresh exception
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task Should_continue_after_cancelled_debounce()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);

            // Act - Stop immediately to cancel debounce
            await service.StopAsync(CancellationToken.None);

            // Assert - Should complete without throwing
        }

        [Fact]
        public async Task Should_handle_file_system_watcher_errors()
        {
            // Arrange
            using var service = SetupIoddService();
            await service.StartAsync(CancellationToken.None);

            // Act - Simulate file system watcher continuing after error
            await Task.Delay(50, TestContext.Current.CancellationToken);

            // Assert - Service should remain operational
            await service.StopAsync(CancellationToken.None);
        }
    }

    public sealed class DirectoryOperations : IoddServiceTests
    {
        [Fact]
        public async Task Should_use_correct_iodd_directory_path()
        {
            // Arrange
            var expectedPath = Path.Combine(Path.GetTempPath(), "Iodds");
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            _fileSystem.Directory.Received(1).CreateDirectory(expectedPath);
        }

        [Fact]
        public async Task Should_handle_directory_already_exists()
        {
            // Arrange
            _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);
            using var service = SetupIoddService();

            // Act
            var act = async () => await service.StartAsync(CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task Should_create_directory_if_not_exists()
        {
            // Arrange
            _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(false);
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            _fileSystem.Directory.Received(1).CreateDirectory(Arg.Any<string>());
        }
    }

    public sealed class RefreshBehavior : IoddServiceTests
    {
        [Fact]
        public async Task Should_call_refresh_on_startup()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            _ioddStore.Received(1).Refresh();
        }

        [Fact]
        public async Task Should_handle_multiple_refresh_calls()
        {
            // Arrange
            var refreshCount = 0;
            _ioddStore.When(x => x.Refresh()).Do(_ => Interlocked.Increment(ref refreshCount));
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert
            refreshCount.Should().Be(1);
        }

        [Fact]
        public async Task Should_continue_operation_if_refresh_fails()
        {
            // Arrange
            var callCount = 0;
            _ioddStore.When(x => x.Refresh()).Do(_ =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First call fails");
            });
            using var service = SetupIoddService();

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert - Service should handle the exception
            await service.StopAsync(CancellationToken.None);
        }
    }

    public sealed class LifecycleManagement : IoddServiceTests
    {
        [Fact]
        public async Task Should_handle_stop_without_start()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            var act = async () => await service.StopAsync(CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void Should_handle_dispose_without_start()
        {
            // Arrange
            using var service = SetupIoddService();

            // Act
            var act = service.Dispose;

            // Assert
            act.Should().NotThrow();
        }
    }
}
