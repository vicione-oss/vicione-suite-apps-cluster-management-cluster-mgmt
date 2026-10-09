using Bunit;
using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.SpinEdit;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaClientConnectionSettingsTests
{
    [Fact]
    public async Task Should_show_the_trusted_certificate_paths()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // the spin edits need services of their own and are not part of what is tested here
        ctx.ComponentFactories.AddStub(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SpinEdit<,,>));

        // Act
        var component = ctx.Render<OpcUaClientConnectionSettings>(parameters => parameters.Add(c => c.Connection, new OpcUaClientConnection
        {
            TrustedCertificatesPath = "trusted",
            TrustedIssuerCertificatesPath = "trusted-issuer",
        }));

        // Assert
        component.Markup.Should().Contain(OpcUaConnectionSettings.TrustedServerCertificatesPath);
        component.Markup.Should().Contain(OpcUaConnectionSettings.TrustedIssuerCertificatesPath);
    }
}
