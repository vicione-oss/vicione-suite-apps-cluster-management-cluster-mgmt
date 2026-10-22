using Bunit;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.Modules;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace ClusterManagement.Client.Tests.IoddManagement.ControlPanels;

internal static class IoddControlPanelTestServices
{
    public static BunitContext SetupIoddControlPanel(this BunitContext ctx, IIoddService ioddService, IMessageBannerService bannerService,
        IUploadTicketFactory uploadTicketFactory, IStreamUploadHandler<IoddUpload> streamUploadHandler)
    {
        ctx.SetupSuiteServices();
        ctx.SetupControlPanelServices();
        ctx.Services.AddDialog();
        ctx.Services.AddGridItemSelectColumn();

        ctx.Services.AddIoddManagement(ModuleIdResolver.ResolveId<ClusterManagementClientModule>());

        ctx.Services.AddSingleton(ioddService);
        ctx.Services.AddSingleton(bannerService);
        ctx.Services.AddSingleton(uploadTicketFactory);
        ctx.Services.AddSingleton(streamUploadHandler);
        ctx.Services.AddSingleton(Substitute.For<IMonochromeIconSvgMarkupProvider>());
        ctx.Services.AddSingleton(Substitute.For<IJsInterop>());
        ctx.Services.AddKeyedSingleton(Sdk.Constants.ClientTimeProviderServiceKey, TimeProvider.System);

        // The upload field and the device list are on the "Manual" page, so that page has to be the active one.
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

    // TreeEditor.Builder 2.0.1 refreshes nodes from a 25 ms timer on the thread pool, and NodeTemplate
    // handles the refresh in an async void method. A refresh that runs while the context is disposed
    // throws a NullReferenceException there, which crashes the test host. Let it run before disposing.
    public static Task SettleTreeAsync() => Task.Delay(150, Xunit.TestContext.Current.CancellationToken);
}
