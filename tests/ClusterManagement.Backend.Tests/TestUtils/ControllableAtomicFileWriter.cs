using System.IO.Abstractions;
using Sdk.Backend.IO;

namespace ClusterManagement.Backend.Tests.TestUtils;

/// <summary>
/// <see cref="TestAtomicFileWriter"/> that can hold or fail the next write of a matching file,
/// so a test can run a second operation while the first one is between its writes.
/// </summary>
internal sealed class ControllableAtomicFileWriter(IFileSystem fileSystem) : IAtomicFileWriter
{
    private readonly TestAtomicFileWriter _writer = new(fileSystem);
    private readonly Lock _lock = new();
    private Func<string, bool>? _holdMatch;
    private Func<string, bool>? _failMatch;
    private TaskCompletionSource _held = NewSignal();
    private TaskCompletionSource _released = NewSignal();

    /// <summary>
    /// Completes when the held write is reached.
    /// </summary>
    public Task Held => _held.Task;

    /// <summary>
    /// Holds the next write of a file matching <paramref name="match"/> until <see cref="Release"/>.
    /// </summary>
    public void HoldNext(Func<string, bool> match)
    {
        lock (_lock)
        {
            _holdMatch = match;
            _held = NewSignal();
            _released = NewSignal();
        }
    }

    public void Release() => _released.TrySetResult();

    /// <summary>
    /// Fails the next write of a file matching <paramref name="match"/> without touching the file.
    /// </summary>
    public void FailNext(Func<string, bool> match)
    {
        lock (_lock)
            _failMatch = match;
    }

    public async Task WriteAsync(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken = default)
    {
        bool hold;
        bool fail;
        TaskCompletionSource held;
        TaskCompletionSource released;

        lock (_lock)
        {
            hold = _holdMatch?.Invoke(filePath) == true;
            fail = !hold && _failMatch?.Invoke(filePath) == true;
            if (hold)
                _holdMatch = null;
            if (fail)
                _failMatch = null;
            held = _held;
            released = _released;
        }

        if (fail)
            throw new IOException($"Write of {filePath} failed");

        if (hold)
        {
            held.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        await _writer.WriteAsync(filePath, writeContent, cancellationToken);
    }

    public static bool IsSnapshot(string filePath) => filePath.EndsWith(".gz", StringComparison.Ordinal);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
