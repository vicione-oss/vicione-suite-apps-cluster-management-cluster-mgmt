using Bunit;
using Bunit.TestDoubles;
using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.SpinEdit;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaClientConnectionSettingsTests
{
    [Fact]
    public async Task Should_show_the_certificate_stores()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // the spin edits need services of their own and are not part of what is tested here
        ctx.ComponentFactories.AddStub(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SpinEdit<,,>));
        ctx.ComponentFactories.AddStub<OpcUaCertificateStoreField>();

        // Act
        var component = ctx.Render<OpcUaClientConnectionSettings>(parameters => parameters.Add(c => c.Connection, new OpcUaClientConnection
        {
            ApplicationCertificatesPath = "application",
            TrustedCertificatesPath = "trusted",
            TrustedIssuerCertificatesPath = "trusted-issuer",
        }));

        // Assert
        component.FindComponents<Stub<OpcUaCertificateStoreField>>()
            .Select(stub => (stub.Instance.Parameters.Get(c => c.StorePath), stub.Instance.Parameters.TryGetValue(nameof(OpcUaCertificateStoreField.AllowUpload), out var allowUpload) && (bool)allowUpload))
            .Should().Equal(("application", true), ("trusted", true), ("trusted-issuer", true));
        component.Markup.Should().Contain(OpcUaConnectionSettings.CertificatesApplyImmediately);
    }
}
