using System.Globalization;
using Bunit;
using ClusterManagement.Client.IoddManagement.ControlPanels;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Iodds.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;

namespace ClusterManagement.Client.Tests.IoddManagement.ControlPanels;

public class IoddControlPanelUploadTests
{
    private const string DestinationFile = "/cache/newIodd/0123456789abcdef.zip";

    private readonly IIoddService _ioddService = Substitute.For<IIoddService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly IUploadTicketFactory _uploadTicketFactory = Substitute.For<IUploadTicketFactory>();
    private readonly IStreamUploadHandler<IoddUpload> _streamUploadHandler = Substitute.For<IStreamUploadHandler<IoddUpload>>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupIoddControlPanel(_ioddService, _bannerService, _uploadTicketFactory, _streamUploadHandler);

        return ctx;
    }

    private static IRenderedComponent<IoddControlPanel> Render(BunitContext ctx)
        => ctx.Render<IoddControlPanel>(p => p.Add(c => c.State, new IoddControlPanelState()));

    private static async Task CompleteUpload(IRenderedComponent<IoddControlPanel> component)
    {
        var upload = component.FindComponent<SettingsFieldFileUpload<IoddUpload>>();

        await component.InvokeAsync(() => upload.Instance.OnUploadSuccess.InvokeAsync(new StreamUploadSuccessResult(DestinationFile)));
    }

    [Fact]
    public async Task Should_add_uploaded_iodd_without_error_banner()
    {
        // Arrange
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns(new IoddServiceSuccessResult());

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        await _ioddService.Received(1).AddIodd(DestinationFile, Arg.Any<CancellationToken>());
        _bannerService.DidNotReceive().ShowMessageBanner(MessageType.Error, Arg.Any<string>());

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    public static TheoryData<int, string> UploadErrors => new()
    {
        { IoddChanged.ArchiveTooLarge, Localization.Common.ArchiveTooLarge },
        { IoddChanged.ExtractZipError, Localization.Common.UploadFailed },
        { IoddChanged.IoddFileNotFound, Localization.Common.UploadFailed },
        { IoddChanged.IoddFileOutsideUploadDirectory, Localization.Common.UploadFailed },
        // Without the existing device there is nothing to name.
        { IoddChanged.IoddAlreadyExists, Localization.Common.UploadFailed },
    };

    [Theory]
    [MemberData(nameof(UploadErrors))]
    public async Task Should_show_localized_message_for_backend_error(int errorCode, string expectedMessage)
    {
        // Arrange
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns(new IoddServiceErrorResult("technical details", errorCode));

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, expectedMessage);

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    [Fact]
    public async Task Should_show_localized_message_naming_the_device_when_iodd_already_exists()
    {
        // Arrange
        var existingDevice = DeviceInfoFactory.CreateDeviceInfo();
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>())
            .Returns(new IoddServiceErrorResult("technical details", IoddChanged.IoddAlreadyExists, existingDevice));

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        var expected = string.Format(CultureInfo.CurrentCulture, Client.IoddManagement.Localization.IoddControlPanel.IoddAlreadyExists, "Acme Sensor A…");
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, expected);

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    [Fact]
    public async Task Should_show_backend_message_for_unknown_error()
    {
        // Arrange
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns(new IoddServiceErrorResult("backend message"));

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, "backend message");

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    [Fact]
    public async Task Should_show_message_and_keep_field_usable_when_adding_throws()
    {
        // Arrange
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns<IIoddServiceResult>(_ => throw new TimeoutException("timed out"));

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, "timed out");

        // A second upload still reaches the service.
        _ioddService.ClearReceivedCalls();
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns(new IoddServiceSuccessResult());
        await CompleteUpload(component);
        await _ioddService.Received(1).AddIodd(DestinationFile, Arg.Any<CancellationToken>());

        await IoddControlPanelTestServices.SettleTreeAsync();
    }

    [Fact]
    public async Task Should_show_message_when_upload_fails()
    {
        // Arrange
        await using var ctx = SetupContext();
        var component = Render(ctx);
        var upload = component.FindComponent<SettingsFieldFileUpload<IoddUpload>>();

        // Act
        await component.InvokeAsync(() => upload.Instance.OnUploadError.InvokeAsync(new StreamUploadErrorResult("disk full")));

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, "disk full");
        await _ioddService.DidNotReceive().AddIodd(Arg.Any<string>(), Arg.Any<CancellationToken>());

        await IoddControlPanelTestServices.SettleTreeAsync();
    }
}
