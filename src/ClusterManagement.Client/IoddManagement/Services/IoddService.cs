using System.Collections.Concurrent;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using ClusterManagement.Public.Iodds.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.IoddManagement.Services;

public sealed class IoddService : CompletionSourceHandlerBase<IIoddServiceResult>,
    IEventConsumer<IoddChanged>,
    IEventConsumer<AutoIoddDownloadChanged>,
    IIoddService
{
    public event Func<IoddChanged, CancellationToken, Task>? IoddChanged;

    public event Func<AutoIoddDownloadChanged, CancellationToken, Task>? AutoIoddDownloadChanged;

    private readonly ConcurrentDictionary<Guid, DeviceInfo> _existingDevices = [];

    /// <summary>
    /// The number of existing devices kept for <see cref="AddIodd"/> calls that have not picked them up yet.
    /// </summary>
    internal int ExistingDeviceCount => _existingDevices.Count;

    public IoddService(IUiMediator mediator) : base(mediator)
    {
        Register<IoddChanged>();
        Register<AutoIoddDownloadChanged>();
    }

    public async Task<IIoddServiceResult> AddIodd(string filename, CancellationToken cancellationToken = default)
    {
        var command = new AddIodd(filename);
        try
        {
            var result = await SendAndWaitForCompletion(command, cancellationToken);

            return result is IoddServiceErrorResult error && _existingDevices.TryGetValue(command.CorrelationId, out var existingDevice)
                ? error with { ExistingDevice = existingDevice }
                : result;
        }
        finally
        {
            _existingDevices.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task<IIoddServiceResult> DeleteIodd(int vendorId, int deviceId, CancellationToken cancellationToken = default)
    {
        var command = new DeleteIodd(vendorId, deviceId);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<(byte[], string)> DownloadIodds(int vendorId, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken = default)
    {
        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = deviceIds };
        var response = await Mediator.Request<DownloadIoddRequest, DownloadIoddResponse>(request, cancellationToken);

        return (response.Content, response.Filename);
    }

    public async Task<IEnumerable<DeviceView>> GetIODevices(CancellationToken cancellationToken = default)
    {
        var request = new GetIODevicesRequest();
        var response = await Mediator.Request<GetIODevicesRequest, GetIODevicesResponse>(request, cancellationToken);

        return response.IODevices.SelectMany(k => k.CreateDeviceVariants());
    }

    public async Task<string> GetIODeviceImage(int vendorId, int deviceId, string imageFilename, CancellationToken cancellationToken = default)
    {
        var request = new GetIODeviceImageRequest
        {
            DeviceId = deviceId,
            VendorId = vendorId,
            ImageFilename = imageFilename
        };

        var response = await Mediator.Request<GetIODeviceImageRequest, GetIODeviceImageResponse>(request, cancellationToken);
        if (response.RequestError is not null)
            return string.Empty;

        return response.ImageAsBase64;
    }

    public async Task<bool> GetIsAutoDownloading(CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Request<GetAutoIoddDownloadRequest, GetAutoIoddDownloadResponse>(new GetAutoIoddDownloadRequest(), cancellationToken);
        return result.IsAutoDownload;
    }

    public async Task<IIoddServiceResult> SetIsAutoDownloading(bool value, CancellationToken cancellationToken = default)
    {
        var command = new SetAutoIoddDownload(value);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<IoddChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            // The error result only carries message and code, so AddIodd picks the existing device up from here.
            if (context.Message.Error.ErrorCode == Public.Iodds.Events.IoddChanged.IoddAlreadyExists && context.Message.DeviceInfo is not null)
                _existingDevices[context.Message.CorrelationId] = context.Message.DeviceInfo;

            // Errors of other clients' uploads, or ones arriving after AddIodd timed out, have no waiting call to remove the device.
            if (!CompleteWithError(context.Message.CorrelationId, context.Message.Error))
                _existingDevices.TryRemove(context.Message.CorrelationId, out _);

            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        await NotifyIoddChanged(context.Message, cancellationToken);
    }

    public async Task Consume(ClientContext<AutoIoddDownloadChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        await NotifyAutoIoddDownloadChanged(context.Message, cancellationToken);
    }

    protected override IIoddServiceResult CreateSuccessResult() => new IoddServiceSuccessResult();

    protected override IIoddServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new IoddServiceErrorResult(errorMessage, errorCode);

    private async Task NotifyIoddChanged(IoddChanged change, CancellationToken cancellationToken)
    {
        if (IoddChanged is not null)
            await IoddChanged.Invoke(change, cancellationToken);
    }

    private async Task NotifyAutoIoddDownloadChanged(AutoIoddDownloadChanged change, CancellationToken cancellationToken)
    {
        if (AutoIoddDownloadChanged is not null)
            await AutoIoddDownloadChanged.Invoke(change, cancellationToken);
    }
}
