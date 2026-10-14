using Bunit;
using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Messaging;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Button;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaCertificateStoreFieldTests : IAsyncDisposable
{
    private const string StorePath = "ua-certs/0123/trusted";
    private const string DestinationFile = "/cache/newUaCertificate/0123456789abcdef.cert";

    private static readonly OpcUaCertificateInfo s_certificate = new("0A1B", "Machine 1", "CN=Machine 1", "CN=Machine 1",
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    private static readonly OpcUaCertificateInfo s_expiredCertificate = new("2C3D", "Machine 2", "CN=Machine 2", "CN=Machine 2",
        DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddYears(-1));

    private readonly BunitContext _ctx = new();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    public OpcUaCertificateStoreFieldTests()
    {
        _ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        _ctx.Services.AddSingleton(_mediator);
        _ctx.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
        _ctx.Services.AddSingleton(Substitute.For<IStreamUploadHandler<OpcUaCertificateUpload>>());
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ReturnCertificates(s_certificate);
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private void ReturnCertificates(params OpcUaCertificateInfo[] certificates)
        => _mediator.Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(Arg.Any<GetOpcUaCertificatesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetOpcUaCertificatesResponse(certificates));

    private IRenderedComponent<OpcUaCertificateStoreField> Render(bool allowUpload = true)
        => _ctx.Render<OpcUaCertificateStoreField>(parameters => parameters
            .Add(c => c.Label, "Trusted certificates")
            .Add(c => c.StorePath, StorePath)
            .Add(c => c.AllowUpload, allowUpload));

    private void VerifyCertificatesRequested(int times)
        => _mediator.Received(times).Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(Arg.Any<GetOpcUaCertificatesRequest>(), Arg.Any<CancellationToken>());

    private void ReturnOnAdd(AddOpcUaCertificateResponse response)
        => _mediator.Request<AddOpcUaCertificateRequest, AddOpcUaCertificateResponse>(Arg.Any<AddOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

    private static void Click(IRenderedComponent<OpcUaCertificateStoreField> component, string titleOrText)
        => component.FindComponents<Button>()
            .Single(b => b.Instance.Title == titleOrText || b.Instance.Text == titleOrText)
            .Find("button").Click();

    private static Task CompleteUpload(IRenderedComponent<OpcUaCertificateStoreField> component)
    {
        var upload = component.FindComponent<SettingsFieldFileUpload<OpcUaCertificateUpload>>();

        return component.InvokeAsync(() => upload.Instance.OnUploadSuccess.InvokeAsync(new StreamUploadSuccessResult(DestinationFile)));
    }

    [Fact]
    public void Should_list_the_certificates_of_the_store()
    {
        // Arrange
        ReturnCertificates(s_certificate, s_expiredCertificate);

        // Act
        var component = Render();

        // Assert
        _mediator.Received(1).Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(new(StorePath), Arg.Any<CancellationToken>());
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("Machine 1", "Machine 2");
        component.FindAll(".certificate--expired").Should().ContainSingle();
    }

    [Fact]
    public void Should_tell_that_the_store_has_no_certificates()
    {
        // Arrange
        ReturnCertificates();

        // Act
        var component = Render();

        // Assert
        component.Find(".certificate--empty").TextContent.Should().Be(OpcUaConnectionSettings.NoCertificates);
    }

    [Fact]
    public void Should_show_an_error_if_the_certificates_cannot_be_loaded()
    {
        // Arrange
        _mediator.Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(Arg.Any<GetOpcUaCertificatesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetOpcUaCertificatesResponse([]) { RequestError = new ErrorInfo(OpcUaCertificateError.InvalidStorePath, "invalid") });

        // Act
        var component = Render();

        // Assert
        component.Find(".certificate-message--error").TextContent.Should().Be(OpcUaConnectionSettings.CertificatesLoadFailed);
    }

    [Fact]
    public void Should_delete_a_certificate_after_a_confirmation_without_loading_the_store_again()
    {
        // Arrange
        _mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteOpcUaCertificateResponse());
        var component = Render();

        // Act
        Click(component, OpcUaConnectionSettings.DeleteCertificate);
        Click(component, CommonVocabulary.Delete);

        // Assert
        _mediator.Received(1).Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(new(StorePath, s_certificate.Thumbprint), Arg.Any<CancellationToken>());
        component.WaitForAssertion(() => component.Find(".certificate--empty").Should().NotBeNull());
        VerifyCertificatesRequested(1);
    }

    [Fact]
    public void Should_load_the_store_again_if_deleting_fails()
    {
        // Arrange
        _mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteOpcUaCertificateResponse { RequestError = new ErrorInfo(OpcUaCertificateError.CertificateNotFound, "not found") });
        var component = Render();

        // Act
        Click(component, OpcUaConnectionSettings.DeleteCertificate);
        Click(component, CommonVocabulary.Delete);

        // Assert
        component.WaitForAssertion(() => component.Find(".certificate-message--error").TextContent.Should().Be(OpcUaConnectionSettings.CertificateDeleteFailed));
        VerifyCertificatesRequested(2);
    }

    [Fact]
    public void Should_not_delete_a_certificate_if_the_confirmation_is_cancelled()
    {
        // Arrange
        var component = Render();

        // Act
        Click(component, OpcUaConnectionSettings.DeleteCertificate);
        Click(component, CommonVocabulary.Cancel);

        // Assert
        _mediator.DidNotReceive().Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>());
        component.FindAll(".certificate-delete-confirmation").Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_an_uploaded_certificate_to_the_store_without_loading_it_again()
    {
        // Arrange
        ReturnOnAdd(new AddOpcUaCertificateResponse([s_expiredCertificate], false));
        var component = Render();

        // Act
        await CompleteUpload(component);

        // Assert
        await _mediator.Received(1).Request<AddOpcUaCertificateRequest, AddOpcUaCertificateResponse>(new(StorePath, DestinationFile), Arg.Any<CancellationToken>());
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("Machine 1", "Machine 2");
        component.FindAll(".certificate-message--error").Should().BeEmpty();
        VerifyCertificatesRequested(1);
    }

    [Fact]
    public async Task Should_list_all_certificates_of_an_uploaded_chain()
    {
        // Arrange
        var issuer = new OpcUaCertificateInfo("4E5F", "Root CA", "CN=Root CA", "CN=Root CA", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        ReturnOnAdd(new AddOpcUaCertificateResponse([s_expiredCertificate, issuer, s_certificate], false));
        var component = Render();

        // Act
        await CompleteUpload(component);

        // Assert
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("Machine 1", "Machine 2", "Root CA");
    }

    [Fact]
    public async Task Should_point_to_the_trusted_issuer_store_if_CA_certificates_of_a_chain_were_left_out()
    {
        // Arrange
        ReturnOnAdd(new AddOpcUaCertificateResponse([s_expiredCertificate], true));
        var component = Render();

        // Act
        await CompleteUpload(component);

        // Assert
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("Machine 1", "Machine 2");
        component.Find(".certificate-message--warning").TextContent.Trim().Should().Be(OpcUaConnectionSettings.IssuerCertificatesLeftOut);
    }

    [Fact]
    public async Task Should_load_the_certificates_again_if_the_upload_fails()
    {
        // Arrange
        ReturnOnAdd(new AddOpcUaCertificateResponse([], false) { RequestError = new ErrorInfo(OpcUaCertificateError.UploadedFileNotFound, "technical details") });
        var component = Render();
        ReturnCertificates(s_certificate, s_expiredCertificate);

        // Act
        await CompleteUpload(component);

        // Assert
        VerifyCertificatesRequested(2);
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("Machine 1", "Machine 2");
        component.Find(".certificate-message--error").TextContent.Should().Be(Localization.Common.UploadFailed);
    }

    [Fact]
    public async Task Should_list_a_certificate_uploaded_again_only_once()
    {
        // Arrange
        ReturnOnAdd(new AddOpcUaCertificateResponse([s_certificate], false));
        var component = Render();

        // Act
        await CompleteUpload(component);

        // Assert
        component.FindAll(".certificate-name").Should().ContainSingle();
    }

    [Theory]
    [InlineData(OpcUaCertificateError.InvalidCertificate, nameof(OpcUaConnectionSettings.InvalidCertificate))]
    [InlineData(OpcUaCertificateError.UploadedFileNotFound, nameof(Localization.Common.UploadFailed))]
    public async Task Should_show_a_localized_message_if_the_upload_fails(int errorCode, string messageName)
    {
        // Arrange
        ReturnOnAdd(new AddOpcUaCertificateResponse([], false) { RequestError = new ErrorInfo(errorCode, "technical details") });
        var component = Render();

        // Act
        await CompleteUpload(component);

        // Assert
        var message = messageName == nameof(OpcUaConnectionSettings.InvalidCertificate)
            ? OpcUaConnectionSettings.InvalidCertificate
            : Localization.Common.UploadFailed;
        component.Find(".certificate-message--error").TextContent.Should().Be(message);
    }

    private const string ApplicationStorePath = "ua-certs/0123/application";

    private static readonly OpcUaCertificateInfo s_applicationCertificate = new("4E5F", "OPC-UA Server", "CN=OPC-UA Server, DC=machine", "CN=OPC-UA Server, DC=machine",
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), "urn:machine:OPCUA:Server");

    private IRenderedComponent<OpcUaCertificateStoreField> RenderApplicationStore()
        => _ctx.Render<OpcUaCertificateStoreField>(parameters => parameters
            .Add(c => c.Label, "Application certificate")
            .Add(c => c.StorePath, ApplicationStorePath)
            .Add(c => c.AllowUpload, true)
            .Add(c => c.IsApplicationStore, true)
            .Add(c => c.ApplicationCertificateSubject, "CN=OPC-UA Server, DC=localhost")
            .Add(c => c.ApplicationUri, "urn:localhost:OPCUA:Server"));

    private void ReturnOnApplicationCertificateAdd(AddOpcUaApplicationCertificateResponse response)
        => _mediator.Request<AddOpcUaApplicationCertificateRequest, AddOpcUaApplicationCertificateResponse>(Arg.Any<AddOpcUaApplicationCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

    [Fact]
    public void Should_show_the_subject_and_URI_of_the_application_certificate()
    {
        // Arrange
        ReturnCertificates(s_applicationCertificate);

        // Act
        var component = RenderApplicationStore();

        // Assert
        component.FindAll(".certificate-detail").Select(e => e.TextContent).Should().Equal(
            "Subject: CN=OPC-UA Server, DC=machine", "URI: urn:machine:OPCUA:Server");
    }

    [Fact]
    public async Task Should_add_an_uploaded_application_certificate_with_the_password_and_the_connection_settings()
    {
        // Arrange
        ReturnCertificates();
        ReturnOnApplicationCertificateAdd(new AddOpcUaApplicationCertificateResponse(s_applicationCertificate, []));
        var component = RenderApplicationStore();
        component.Find("input[type=password]").Input("secret");
        component.Find("input[type=password]").Blur();

        // Act
        await CompleteUpload(component);

        // Assert
        await _mediator.Received(1).Request<AddOpcUaApplicationCertificateRequest, AddOpcUaApplicationCertificateResponse>(
            new(ApplicationStorePath, DestinationFile, "secret", "CN=OPC-UA Server, DC=localhost", "urn:localhost:OPCUA:Server"), Arg.Any<CancellationToken>());
        component.FindAll(".certificate-name").Select(e => e.TextContent).Should().Equal("OPC-UA Server");
        component.FindAll(".certificate-message").Should().BeEmpty();
    }

    [Fact]
    public async Task Should_warn_if_the_uploaded_application_certificate_does_not_match_the_connection()
    {
        // Arrange
        ReturnCertificates();
        ReturnOnApplicationCertificateAdd(new AddOpcUaApplicationCertificateResponse(s_applicationCertificate,
            [OpcUaApplicationCertificateWarning.SubjectMismatch, OpcUaApplicationCertificateWarning.ApplicationUriMismatch]));
        var component = RenderApplicationStore();

        // Act
        await CompleteUpload(component);

        // Assert
        component.FindAll(".certificate-message--warning").Select(e => e.TextContent.Trim()).Should().Equal(
            OpcUaConnectionSettings.ApplicationCertificateSubjectMismatch, OpcUaConnectionSettings.ApplicationCertificateUriMismatch);
        component.FindAll(".certificate-name").Should().ContainSingle();
    }

    [Theory]
    [InlineData(OpcUaCertificateError.InvalidApplicationCertificate)]
    [InlineData(OpcUaCertificateError.NoPrivateKey)]
    public async Task Should_show_a_localized_message_if_the_application_certificate_upload_fails(int errorCode)
    {
        // Arrange
        ReturnCertificates();
        ReturnOnApplicationCertificateAdd(new AddOpcUaApplicationCertificateResponse(null, []) { RequestError = new ErrorInfo(errorCode, "technical details") });
        var component = RenderApplicationStore();

        // Act
        await CompleteUpload(component);

        // Assert
        component.Find(".certificate-message--error").TextContent.Should().Be(errorCode == OpcUaCertificateError.NoPrivateKey
            ? OpcUaConnectionSettings.NoPrivateKey
            : OpcUaConnectionSettings.InvalidApplicationCertificate);
        component.FindAll(".certificate-name").Should().BeEmpty();
    }

    [Fact]
    public void Should_lock_the_upload_while_the_store_has_an_application_certificate()
    {
        // Arrange
        ReturnCertificates(s_applicationCertificate);

        // Act
        var component = RenderApplicationStore();

        // Assert
        component.FindComponents<SettingsFieldFileUpload<OpcUaCertificateUpload>>().Should().BeEmpty();
        component.FindAll("input[type=password]").Should().BeEmpty();
        component.FindComponents<Button>().Single(b => b.Instance.Text == OpcUaConnectionSettings.UploadCertificate).Instance.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Should_unlock_the_upload_once_the_application_certificate_is_deleted()
    {
        // Arrange
        ReturnCertificates(s_applicationCertificate);
        _mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteOpcUaCertificateResponse());
        var component = RenderApplicationStore();

        // Act
        Click(component, OpcUaConnectionSettings.DeleteCertificate);
        Click(component, CommonVocabulary.Delete);

        // Assert
        component.WaitForAssertion(() => component.FindComponents<SettingsFieldFileUpload<OpcUaCertificateUpload>>().Should().ContainSingle());
    }

    [Fact]
    public void Should_not_lock_the_upload_of_a_trusted_store_with_certificates()
        => Render().FindComponents<SettingsFieldFileUpload<OpcUaCertificateUpload>>().Should().ContainSingle();

    [Fact]
    public async Task Should_delete_all_certificates_of_the_store()
    {
        // Arrange
        ReturnCertificates(s_certificate, s_expiredCertificate);
        _mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteOpcUaCertificateResponse());
        var component = Render();

        // Act
        await component.InvokeAsync(component.Instance.DeleteAllAsync);

        // Assert
        component.Instance.HasCertificates.Should().BeFalse();
        await _mediator.Received(2).Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(Arg.Any<DeleteOpcUaCertificateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Should_offer_no_upload_if_not_allowed()
    {
        // Act
        var component = Render(allowUpload: false);

        // Assert
        component.FindComponents<SettingsFieldFileUpload<OpcUaCertificateUpload>>().Should().BeEmpty();
    }
}
