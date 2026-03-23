using System.Globalization;
using System.IO.Abstractions;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetIODeviceImageConsumer(IIoddStore ioddStore, IFileSystem fileSystem)
    : RequestConsumer<GetIODeviceImageRequest, GetIODeviceImageResponse>
{
    public override Task<GetIODeviceImageResponse> HandleException(GetIODeviceImageRequest message, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetIODeviceImageResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}"), ImageAsBase64 = string.Empty, });

    public override async Task<GetIODeviceImageResponse> Respond(GetIODeviceImageRequest message, CancellationToken cancellationToken)
    {
        var imageFilePath = fileSystem.Path.Combine(ioddStore.IoddDirectory,
            message.VendorId.ToString(CultureInfo.InvariantCulture),
            message.DeviceId.ToString(CultureInfo.InvariantCulture),
            message.ImageFilename);

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
