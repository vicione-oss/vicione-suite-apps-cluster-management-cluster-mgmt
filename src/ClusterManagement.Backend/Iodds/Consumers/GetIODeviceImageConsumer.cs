using System.Globalization;
using System.IO.Abstractions;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Requests;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetIODeviceImageConsumer(IIoddStore ioddStore, IFileSystem fileSystem) : RequestConsumer<GetIODeviceImageConsumerRequest, GetIODeviceImageResponse>
{
    protected override Task<GetIODeviceImageResponse> HandleException(ConsumeContext<GetIODeviceImageConsumerRequest> context, Exception e)
        => Task.FromResult(new GetIODeviceImageResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}"), ImageAsBase64 = string.Empty, });

    protected override async Task<GetIODeviceImageResponse> Respond(ConsumeContext<GetIODeviceImageConsumerRequest> context)
    {
        var imageFilePath = fileSystem.Path.Combine(ioddStore.IoddDirectory,
            context.Message.VendorId.ToString(CultureInfo.InvariantCulture),
            context.Message.DeviceId.ToString(CultureInfo.InvariantCulture),
            context.Message.ImageFilename);

        if (!fileSystem.File.Exists(imageFilePath))
            throw new InvalidOperationException("Image file does not exist.");

        return new() { ImageAsBase64 = await ImageStreamToBase64(imageFilePath) };
    }

    private async Task<string> ImageStreamToBase64(string path)
    {
        await using var stream = fileSystem.File.OpenRead(path);
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }
}
