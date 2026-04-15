using System.IO.Abstractions;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Iodds;

internal sealed partial class IoddService(IIoddStore ioddStore, IFileSystem fileSystem, ILogger<IoddService> logger) : IHostedService, IDisposable
{
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(10);

    private IFileSystemWatcher? _watcher;
    private DateTimeOffset? _changed;
    private Task? _task;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            fileSystem.Directory.CreateDirectory(ioddStore.IoddDirectory);

            _watcher = fileSystem.FileSystemWatcher.New(ioddStore.IoddDirectory);
            _watcher.Filter = "*.xml";
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.IncludeSubdirectories = true;

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.EnableRaisingEvents = true;

            ioddStore.Refresh();
        }
        catch (Exception ex)
        {
            LogError(logger, ex);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnChanged;
            _watcher.Dispose();
        }

        await _cancellationTokenSource.CancelAsync();

        try
        {
            if (_task is not null)
                await _task;
        }
        catch (OperationCanceledException)
        { }

        _task = null;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => OnChanged();

    private void OnChanged()
    {
        lock (_lock)
        {
            _changed = DateTimeOffset.UtcNow;

            if (_task is null || _task.IsCompleted)
                _task = Task.Run(Debounce);
        }
    }

    private async Task Debounce()
    {
        while (DebounceDelayExceeded())
            await Task.Delay(1_000, _cancellationTokenSource.Token);

        ioddStore.Refresh();

        lock (_lock)
            _changed = null;

        bool DebounceDelayExceeded()
        {
            lock (_lock)
                return _changed is not null && DateTimeOffset.UtcNow - _changed < _debounceDelay;
        }
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnChanged;
            _watcher.Dispose();
        }

        try
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to init IODD file watcher.")]
    private static partial void LogError(ILogger<IoddService> logger, Exception exception);
}
