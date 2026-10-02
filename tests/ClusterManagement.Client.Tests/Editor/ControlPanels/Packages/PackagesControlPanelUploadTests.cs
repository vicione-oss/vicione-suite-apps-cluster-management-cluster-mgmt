using Bunit;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Localization;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Modules;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages;

public class PackagesControlPanelUploadTests
{
    private const string DestinationFile = "/cache/newPackage/Package.zip";

    private readonly IPackageService _packageService = Substitute.For<IPackageService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly IUploadTicketFactory _uploadTicketFactory = Substitute.For<IUploadTicketFactory>();
    private readonly IStreamUploadHandler<ClusterPackageUpload> _streamUploadHandler = Substitute.For<IStreamUploadHandler<ClusterPackageUpload>>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupControlPanelServices();

        ctx.Services.AddSingleton(_packageService);
        ctx.Services.AddSingleton(_bannerService);
        ctx.Services.AddScoped(_ => Substitute.For<IResourceDownloadStateService>());
        ctx.Services.AddPackagesControlPanel(ModuleIdResolver.ResolveId<ClusterManagementClientModule>());

        ctx.Services.AddSingleton(_uploadTicketFactory);
        ctx.Services.AddSingleton(_streamUploadHandler);

        // The upload field is on the "Browse" page, so that page has to be the active one.
        List<IControlPanelPage> pages = [];
        var pageRegistry = Substitute.For<IControlPanelPageRegistry>();
        pageRegistry.When(r => r.Add(Arg.Any<IControlPanelPage>(), Arg.Any<IControlPanelRegistryItem>())).Do(c => pages.Add(c.Arg<IControlPanelPage>()));
        var activePageProvider = Substitute.For<IActiveControlPanelPageProvider>();
        activePageProvider.GetActiveControlPanelPage(Arg.Any<IControlPanelRegistryItem>()).Returns(_ => pages.Find(p => p.Title == CommonVocabulary.BrowseVerb));
        ctx.Services.AddSingleton(pageRegistry);
        ctx.Services.AddSingleton(activePageProvider);
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelDescriptorProvider>());

        // The upload field is only rendered for administrators.
        var authContext = ctx.AddAuthorization();
        authContext.SetAuthorized("admin");
        authContext.SetPolicies(ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full));

        return ctx;
    }

    private static async Task CompleteUpload(IRenderedComponent<PackagesControlPanel> component)
    {
        var upload = component.FindComponent<SettingsFieldFileUpload<ClusterPackageUpload>>();

        await component.InvokeAsync(() => upload.Instance.OnUploadSuccess.InvokeAsync(new StreamUploadSuccessResult(DestinationFile)));
    }

    [Fact]
    public async Task Should_add_uploaded_package_and_ask_for_restart()
    {
        // Arrange
        _packageService.UploadPackage(DestinationFile, Arg.Any<CancellationToken>()).Returns(new PackageServiceSuccessResult());

        using var ctx = SetupContext();
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, new PackagesControlPanelState()));

        // Act
        await CompleteUpload(component);

        // Assert
        await _packageService.Received(1).UploadPackage(DestinationFile, Arg.Any<CancellationToken>());
        _bannerService.Received(1).ShowMessageBanner(MessageType.Warning, Client.Editor.Localization.Common.NeedRestart);
    }

    public static TheoryData<int, string> UploadErrors => new()
    {
        { ClusterPackageChanged.NotAZipArchive, Common.FileTypeMissmatch },
        { ClusterPackageChanged.EmptyArchive, Common.FileIsEmpty },
        { ClusterPackageChanged.PackageVersionInUse, Client.Editor.ControlPanels.Packages.Localization.PackagesControlPanel.PackageVersionInUse },
        { ClusterPackageChanged.CorruptArchive, Common.UploadFailed },
        { ClusterPackageChanged.InvalidPackageContent, Common.UploadFailed },
        { ClusterPackageChanged.PackageFileNotFound, Common.UploadFailed },
        { ClusterPackageChanged.PackageFileOutsideUploadDirectory, Common.UploadFailed },
        { ClusterPackageChanged.AddPackageFailed, Common.UploadFailed },
    };

    [Theory]
    [MemberData(nameof(UploadErrors))]
    public async Task Should_show_localized_message_for_backend_error(int errorCode, string expectedMessage)
    {
        // Arrange
        _packageService.UploadPackage(DestinationFile, Arg.Any<CancellationToken>()).Returns(new PackageServiceErrorResult("technical details", errorCode));

        using var ctx = SetupContext();
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, new PackagesControlPanelState()));

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, expectedMessage);
    }

    [Fact]
    public async Task Should_show_backend_message_for_unknown_error()
    {
        // Arrange
        _packageService.UploadPackage(DestinationFile, Arg.Any<CancellationToken>()).Returns(new PackageServiceErrorResult("backend message"));

        using var ctx = SetupContext();
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, new PackagesControlPanelState()));

        // Act
        await CompleteUpload(component);

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, "backend message");
    }

    [Fact]
    public async Task Should_show_message_when_upload_fails()
    {
        // Arrange
        using var ctx = SetupContext();
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, new PackagesControlPanelState()));
        var upload = component.FindComponent<SettingsFieldFileUpload<ClusterPackageUpload>>();

        // Act
        await component.InvokeAsync(() => upload.Instance.OnUploadError.InvokeAsync(new StreamUploadErrorResult("disk full")));

        // Assert
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, "disk full");
        await _packageService.DidNotReceive().UploadPackage(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
