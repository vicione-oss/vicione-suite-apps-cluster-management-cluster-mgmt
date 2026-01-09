using NSubstitute.Core.Arguments;

namespace ClusterManagement.Backend.Tests.TestUtils;

public static class FluentArg
{
    public static ref T? Assert<T>(Action<T> action)
        => ref ArgumentMatcher.Enqueue(new AssertionMatcher<T>(action));
}
