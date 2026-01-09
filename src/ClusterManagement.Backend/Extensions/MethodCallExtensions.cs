using Sdk.Messaging;

namespace ClusterManagement.Backend.Extensions;

internal static class MethodCallExtensions
{
    internal static async Task CatchAs(this Task subject, Func<Exception, Exception> createException, CancellationToken cancellationToken)
    {
        try
        {
            await subject;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException(ex);
        }
    }

    internal static async Task<T> CatchAs<T>(this Task<T> subject, Func<Exception, Exception> createException, CancellationToken cancellationToken)
    {
        try
        {
            return await subject;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw createException(ex);
        }
    }

    internal static async Task CatchAs(this Task subject, ErrorInfo errorInfo, CancellationToken cancellationToken)
    {
        try
        {
            await subject;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw errorInfo.AsException(ex);
        }
    }

    internal static async Task<T> CatchAs<T>(this Task<T> subject, ErrorInfo errorInfo, CancellationToken cancellationToken)
    {
        try
        {
            return await subject;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw errorInfo.AsException(ex);
        }
    }
}
