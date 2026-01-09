namespace ClusterManagement.Client.Extensions;

internal sealed class AsyncDisposable
{
    internal static IAsyncDisposable Create<TState>(TState state, Func<TState, Task> dispose)
        => new AnonymousAsyncDisposable<TState>(state, dispose);
}

internal sealed class AnonymousAsyncDisposable<TState> : IAsyncDisposable
{
    private TState? _state;

    private volatile Func<TState, Task>? _dispose;

    internal bool IsDisposed => _dispose is null;

    internal AnonymousAsyncDisposable(TState state, Func<TState, Task> dispose)
    {
        _state = state;
        _dispose = dispose;
    }

    public async ValueTask DisposeAsync()
    {
        var dispose = Interlocked.Exchange(ref _dispose, default);
        if (dispose is not null)
            await dispose.Invoke(_state!);
        _state = default;
    }
}
