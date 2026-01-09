using System.IO.Abstractions;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.Hosting;

namespace ClusterManagement.Backend.Iodds;

internal sealed class IoddService(IIoddStore ioddStore, IFileSystem fileSystem) : IHostedService, IDisposable
{
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(10);

    private FileSystemWatcher? _watcher;
    private DateTimeOffset? _changed;
    private Task? _task;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        fileSystem.Directory.CreateDirectory(ioddStore.IoddDirectory);

        _watcher = new(ioddStore.IoddDirectory)
        {
            Filter = "*.xml",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;

        OnChanged();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher is not null)
        {
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

        _cancellationTokenSource.Dispose();
    }
}
