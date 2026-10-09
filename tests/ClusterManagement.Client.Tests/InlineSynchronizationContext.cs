namespace ClusterManagement.Client.Tests;

/// <summary>
/// Runs what is posted to it at once, on the thread that posts it. Code started under it continues there after each
/// await, so a test sees the effect of advancing a fake clock as soon as the advance returns.
/// </summary>
internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    private static readonly InlineSynchronizationContext s_instance = new();

    public static void Run(Action action)
        => s_instance.Send(_ => action(), null);

    public override void Post(SendOrPostCallback d, object? state)
        => Send(d, state);

    public override void Send(SendOrPostCallback d, object? state)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            d(state);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
