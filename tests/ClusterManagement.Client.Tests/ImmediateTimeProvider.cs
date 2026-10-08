namespace ClusterManagement.Client.Tests;

/// <summary>
/// Fires every timer at once, so a timeout, delay or retry does not have to pass in real time.
/// </summary>
internal sealed class ImmediateTimeProvider : TimeProvider
{
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => System.CreateTimer(callback, state, TimeSpan.Zero, period);
}
