using AwesomeAssertions.Execution;
using NSubstitute.Core;
using NSubstitute.Core.Arguments;

namespace ClusterManagement.Backend.Tests.TestUtils;

internal sealed class AssertionMatcher<T>(Action<T> assertion) : IArgumentMatcher<T>, IDescribeNonMatches
{
    private readonly Action<T> _assertion = assertion;
    private Exception? _exception;

    public bool IsSatisfiedBy(T? argument)
    {
        try
        {
            using AssertionScope scope = new();
            _assertion(argument!);
        }
        catch (Exception ex)
        {
            _exception = ex;
        }
        return _exception is null;
    }

    public string DescribeFor(object? argument) => _exception?.Message ?? string.Empty;
}
