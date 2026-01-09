namespace ClusterManagement.Backend.Engines;

internal sealed class ErrorInfoException : InvalidOperationException
{
    public int ErrorCode { get; }

    public ErrorInfoException() { }

    public ErrorInfoException(string message) : base(message) { }

    public ErrorInfoException(string message, Exception innerException)
        : base(message, innerException) { }

    public ErrorInfoException(int errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
        => ErrorCode = errorCode;
}
