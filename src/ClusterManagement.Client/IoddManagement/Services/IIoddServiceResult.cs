namespace ClusterManagement.Client.IoddManagement.Services;

/// <summary>
/// Describes the result of a <see cref="IoddService"/> method call
/// </summary>
public interface IIoddServiceResult;

/// <summary>
/// Describes the result of a successful call to a method of <see cref="IIoddServiceResult"/> 
/// </summary>
public readonly record struct IoddServiceSuccessResult : IIoddServiceResult;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IIoddServiceResult"/> 
/// </summary>
public readonly record struct IoddServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IIoddServiceResult;
