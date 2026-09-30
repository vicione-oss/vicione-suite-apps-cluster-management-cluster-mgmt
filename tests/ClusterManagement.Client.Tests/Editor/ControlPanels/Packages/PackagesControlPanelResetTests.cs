using Bunit;
using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Contracts;
using Sdk.Client.Services;
using Sdk.Modules;
using Sdk.Testing.Client;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages;

public class PackagesControlPanelResetTests
{
    private readonly IPackageService _packageService = Substitute.For<IPackageService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly IUploadTicketFactory _uploadTicketFactory = Substitute.For<IUploadTicketFactory>();
    private readonly IStreamUploadHandler<ClusterPackageUpload> _streamUploadHandler = Substitute.For<IStreamUploadHandler<ClusterPackageUpload>>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.SetupDevExpressBlazor();
        ctx.SetupControlPanelServices();

        ctx.Services.AddSingleton(_packageService);
        ctx.Services.AddSingleton(_bannerService);
        ctx.Services.AddScoped(_ => Substitute.For<IResourceDownloadStateService>());
        ctx.Services.AddPackagesControlPanel(ModuleIdResolver.ResolveId<ClusterManagementClientModule>());

        // AddPackagesControlPanel registers a stream upload handler factory that isn't wired up
        // in the test host, so replace it (and the ticket factory it needs) with substitutes.
        ctx.Services.AddSingleton(_uploadTicketFactory);
        ctx.Services.AddSingleton(_streamUploadHandler);

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new PackagesControlPanelState();

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<PackagesControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }
}
