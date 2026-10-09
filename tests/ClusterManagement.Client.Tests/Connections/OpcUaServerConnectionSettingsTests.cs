using Bunit;
using ClusterManagement.Client.Connections;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Public.Connections.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.SpinEdit;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaServerConnectionSettingsTests : IAsyncDisposable
{
    private readonly BunitContext _ctx = new();

    public OpcUaServerConnectionSettingsTests()
    {
        _ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule>());
        _ctx.Services.AddSingleton(Substitute.For<IUiMediator>());
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // the spin edits need services of their own and are not part of what is tested here
        _ctx.ComponentFactories.AddStub(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SpinEdit<,,>));
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private IRenderedComponent<OpcUaServerConnectionSettings> Render(OpcUaSecurityPolicy securityPolicy)
        => _ctx.Render<OpcUaServerConnectionSettings>(parameters => parameters.Add(component => component.Connection, new OpcUaServerConnection
        {
            SecurityPolicy = securityPolicy,
            TrustedCertificatesPath = "trusted",
            TrustedIssuerCertificatesPath = "trusted-issuer",
        }));

    [Fact]
    public void Should_hide_the_trusted_certificate_paths_without_a_security_policy()
    {
        // Act
        var component = Render(OpcUaSecurityPolicy.None);

        // Assert
        component.Markup.Should().NotContain(OpcUaConnectionSettings.TrustedClientCertificatesPath);
        component.Markup.Should().NotContain(OpcUaConnectionSettings.TrustedIssuerCertificatesPath);
    }

    [Theory]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256Sign)]
    [InlineData(OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt)]
    [InlineData(OpcUaSecurityPolicy.Aes256Sha256RsaPssSignAndEncrypt)]
    public void Should_show_the_trusted_certificate_paths_with_a_security_policy(OpcUaSecurityPolicy securityPolicy)
    {
        // Act
        var component = Render(securityPolicy);

        // Assert
        component.Markup.Should().Contain(OpcUaConnectionSettings.TrustedClientCertificatesPath);
        component.Markup.Should().Contain(OpcUaConnectionSettings.TrustedIssuerCertificatesPath);
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
}
