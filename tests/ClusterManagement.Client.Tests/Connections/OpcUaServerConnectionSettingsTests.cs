using Bunit;
using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Connections.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;
using ViciOne.Ui.Blazor.Components.SpinEdit;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Dialog.Extensions;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaServerConnectionSettingsTests : IAsyncDisposable
{
    private static readonly OpcUaCertificateInfo s_applicationCertificate = new("4E5F", "OPC-UA Server", "CN=OPC-UA Server", "CN=OPC-UA Server",
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), "urn:machine:OPCUA:Server");

    private readonly BunitContext _ctx = new();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    public OpcUaServerConnectionSettingsTests()
    {
        _ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        _ctx.Services.AddSingleton(_mediator);
        _ctx.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
        _ctx.Services.AddSingleton(Substitute.For<IStreamUploadHandler<OpcUaCertificateUpload>>());
        _ctx.Services.AddDialog();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // the spin edits need services of their own and are not part of what is tested here
        _ctx.ComponentFactories.AddStub(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SpinEdit<,,>));

        ReturnApplicationCertificates();
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private void ReturnApplicationCertificates(params OpcUaCertificateInfo[] certificates)
    {
        _mediator.Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(Arg.Any<GetOpcUaCertificatesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetOpcUaCertificatesResponse([]));
        _mediator.Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(new("application"), Arg.Any<CancellationToken>())
            .Returns(new GetOpcUaCertificatesResponse(certificates));
    }

    private IRenderedComponent<OpcUaServerConnectionSettings> Render(OpcUaSecurityPolicy securityPolicy = OpcUaSecurityPolicy.Basic256Sha256Sign)
        => _ctx.Render<OpcUaServerConnectionSettings>(parameters => parameters.Add(component => component.Connection, new OpcUaServerConnection
        {
            SecurityPolicy = securityPolicy,
            NetworkInterface = OpcUaServerConnection.LocalNetworkInterface,
            ApplicationCertificatesPath = "application",
            TrustedCertificatesPath = "trusted",
            TrustedIssuerCertificatesPath = "trusted-issuer",
        }));

    private static IEnumerable<(string? StorePath, bool AllowUpload)> GetCertificateStores(IRenderedComponent<OpcUaServerConnectionSettings> component)
        => component.FindComponents<OpcUaCertificateStoreField>().Select(field => (field.Instance.StorePath, field.Instance.AllowUpload));

    private static Task ChangeNetworkInterface(IRenderedComponent<OpcUaServerConnectionSettings> component, string networkInterface)
    {
        var comboBox = component.FindComponent<SettingsFieldComboBox<ComboBoxItem<string, string>, string>>();
        return component.InvokeAsync(() => comboBox.Instance.ValueChanged.InvokeAsync(networkInterface));
    }

    private static IRenderedComponent<Dialog> NetworkInterfaceChangedDialog(IRenderedComponent<OpcUaServerConnectionSettings> component)
        => component.FindComponents<Dialog>().Single(d => d.Instance.HeaderText == OpcUaConnectionSettings.NetworkInterfaceChanged);

    [Fact]
    public void Should_show_only_the_application_certificate_without_a_security_policy()
    {
        // Act
        var component = Render(OpcUaSecurityPolicy.None);

        // Assert
        GetCertificateStores(component).Should().Equal(("application", true));
    }

    [Theory]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256Sign)]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt)]
    [InlineData(OpcUaSecurityPolicy.Aes256Sha256RsaPssSignAndEncrypt)]
    public void Should_show_all_certificate_stores_with_a_security_policy(OpcUaSecurityPolicy securityPolicy)
    {
        // Act
        var component = Render(securityPolicy);

        // Assert
        GetCertificateStores(component).Should().Equal(("application", true), ("trusted", true), ("trusted-issuer", true));
    }

    [Fact]
    public void Should_warn_without_a_security_policy()
    {
        // Act
        var component = Render(OpcUaSecurityPolicy.None);

        // Assert
        component.Markup.Should().Contain(OpcUaConnectionSettings.SecurityPolicyNoneWarning);
    }

    [Theory]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256Sign)]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt)]
    [InlineData(OpcUaSecurityPolicy.Aes256Sha256RsaPssSignAndEncrypt)]
    public void Should_not_warn_with_a_security_policy(OpcUaSecurityPolicy securityPolicy)
    {
        // Act
        var component = Render(securityPolicy);

        // Assert
        component.Markup.Should().NotContain(OpcUaConnectionSettings.SecurityPolicyNoneWarning);
    }

    [Fact]
    public async Task Should_ask_about_the_application_certificate_when_the_network_interface_changes()
    {
        // Arrange
        ReturnApplicationCertificates(s_applicationCertificate);
        var component = Render();

        // Act
        await ChangeNetworkInterface(component, "eth1");

        // Assert
        NetworkInterfaceChangedDialog(component).Instance.Visible.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_ask_without_an_application_certificate()
    {
        // Arrange
        var component = Render();

        // Act
        await ChangeNetworkInterface(component, "eth1");

        // Assert
        NetworkInterfaceChangedDialog(component).Instance.Visible.Should().BeFalse();
        component.Instance.Connection.NetworkInterface.Should().Be("eth1");
    }

    [Fact]
    public async Task Should_undo_the_network_interface_change()
    {
        // Arrange
        ReturnApplicationCertificates(s_applicationCertificate);
        var component = Render();
        await ChangeNetworkInterface(component, "eth1");

        // Act
        var dialogContent = NetworkInterfaceChangedDialog(component).RenderSectionContent(_ctx);
        await dialogContent.FindFooterButton(b => b.Text == OpcUaConnectionSettings.UndoChange).FindInnerButton().ClickAsync(new());

        // Assert
        component.Instance.Connection.NetworkInterface.Should().Be(OpcUaServerConnection.LocalNetworkInterface);
        NetworkInterfaceChangedDialog(component).Instance.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task Should_delete_the_application_certificate_after_the_network_interface_change()
    {
        // Arrange
        ReturnApplicationCertificates(s_applicationCertificate);
        _mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteOpcUaCertificateResponse());
        var component = Render();
        await ChangeNetworkInterface(component, "eth1");

        // Act
        var dialogContent = NetworkInterfaceChangedDialog(component).RenderSectionContent(_ctx);
        await dialogContent.FindFooterButton(b => b.Text == OpcUaConnectionSettings.DeleteCertificate).FindInnerButton().ClickAsync(new());

        // Assert
        await _mediator.Received(1).Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(new("application", "4E5F"), Arg.Any<CancellationToken>());
        component.Instance.Connection.NetworkInterface.Should().Be("eth1");
    }

    [Fact]
    public async Task Should_keep_the_application_certificate_and_the_new_network_interface()
    {
        // Arrange
        ReturnApplicationCertificates(s_applicationCertificate);
        var component = Render();
        await ChangeNetworkInterface(component, "eth1");

        // Act
        var dialogContent = NetworkInterfaceChangedDialog(component).RenderSectionContent(_ctx);
        await dialogContent.FindFooterButton(b => b.Text == OpcUaConnectionSettings.KeepCertificate).FindInnerButton().ClickAsync(new());

        // Assert
        _ = _mediator.DidNotReceive().Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>());
        component.Instance.Connection.NetworkInterface.Should().Be("eth1");
        NetworkInterfaceChangedDialog(component).Instance.Visible.Should().BeFalse();
    }
}
