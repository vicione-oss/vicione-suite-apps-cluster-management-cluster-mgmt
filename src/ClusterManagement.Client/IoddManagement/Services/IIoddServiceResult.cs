using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;

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
/// <param name="ErrorMessage">The technical error message of the backend.</param>
/// <param name="ErrorCode">The error code of the backend, if any.</param>
/// <param name="ExistingDevice">The device that is already in the store, if the error is <see cref="IoddChanged.IoddAlreadyExists"/>.</param>
public readonly record struct IoddServiceErrorResult(string ErrorMessage, int? ErrorCode = null, DeviceInfo? ExistingDevice = null)
    : IIoddServiceResult;
