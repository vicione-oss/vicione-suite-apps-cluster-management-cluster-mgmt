using Bunit;
using ClusterManagement.Client.IoddManagement.ControlPanels;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Iodds.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Modules;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

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
        ctx.SetupSuiteServices();
        ctx.SetupControlPanelServices();
        ctx.Services.AddDialog();
        ctx.Services.AddGridItemSelectColumn();

        ctx.Services.AddIoddManagement(ModuleIdResolver.ResolveId<ClusterManagementClientModule>());

        ctx.Services.AddSingleton(_ioddService);
        ctx.Services.AddSingleton(_bannerService);
        ctx.Services.AddSingleton(_uploadTicketFactory);
        ctx.Services.AddSingleton(_streamUploadHandler);
        ctx.Services.AddSingleton(Substitute.For<IMonochromeIconSvgMarkupProvider>());
        ctx.Services.AddSingleton(Substitute.For<IJsInterop>());
        ctx.Services.AddKeyedSingleton(Sdk.Constants.ClientTimeProviderServiceKey, TimeProvider.System);

        // The upload field is on the "Manual" page, so that page has to be the active one.
        List<IControlPanelPage> pages = [];
        var pageRegistry = Substitute.For<IControlPanelPageRegistry>();
        pageRegistry.When(r => r.Add(Arg.Any<IControlPanelPage>(), Arg.Any<IControlPanelRegistryItem>())).Do(c => pages.Add(c.Arg<IControlPanelPage>()));
        var activePageProvider = Substitute.For<IActiveControlPanelPageProvider>();
        activePageProvider.GetActiveControlPanelPage(Arg.Any<IControlPanelRegistryItem>()).Returns(_ => pages.Find(p => p.Title == Client.IoddManagement.Localization.IoddControlPanel.Manual));
        ctx.Services.AddSingleton(pageRegistry);
        ctx.Services.AddSingleton(activePageProvider);
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelDescriptorProvider>());

        ctx.AddAuthorization().SetAuthorized("admin");

        return ctx;
    }

    private static IRenderedComponent<IoddControlPanel> Render(BunitContext ctx)
        => ctx.Render<IoddControlPanel>(p => p.Add(c => c.State, new IoddControlPanelState()));

    // TreeEditor.Builder 2.0.1 refreshes nodes from a 25 ms timer on the thread pool, and NodeTemplate
    // handles the refresh in an async void method. A refresh that runs while the context is disposed
    // throws a NullReferenceException there, which crashes the test host. Let it run before disposing.
    private static Task SettleTreeAsync() => Task.Delay(150, Xunit.TestContext.Current.CancellationToken);

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

        await SettleTreeAsync();
    }

    public static TheoryData<int> UploadErrors => [
        IoddChanged.ExtractZipError,
        IoddChanged.IoddFileNotFound,
        IoddChanged.IoddFileOutsideUploadDirectory,
    ];

    [Theory]
    [MemberData(nameof(UploadErrors))]
    public async Task Should_show_localized_message_for_backend_error(int errorCode)
    {
        // Arrange
        _ioddService.AddIodd(DestinationFile, Arg.Any<CancellationToken>()).Returns(new IoddServiceErrorResult("technical details", errorCode));

        await using var ctx = SetupContext();
        var component = Render(ctx);

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, Localization.Common.UploadFailed);

        await SettleTreeAsync();
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

        await SettleTreeAsync();
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

        await SettleTreeAsync();
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

        await SettleTreeAsync();
    }
}
