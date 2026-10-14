using ClusterManagement.Backend.Connections;
using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Backend.Messaging;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Connections;

public sealed class OpcUaCertificateCleanupServiceTests : IDisposable
{
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();
    private readonly IOpcUaCertificateStore _store = Substitute.For<IOpcUaCertificateStore>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ServiceProvider _services;
    private readonly OpcUaCertificateCleanupService _service;

    public OpcUaCertificateCleanupServiceTests()
    {
        _services = new ServiceCollection().AddSingleton(_mediator).AddSingleton(_store).BuildServiceProvider();

        // a cancelled token is one whose application has already started
        _lifetime.ApplicationStarted.Returns(new CancellationToken(true));
        _service = new(_services, _lifetime, NullLogger<OpcUaCertificateCleanupService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        _services.Dispose();
    }

    private async Task RunAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await _service.ExecuteTask!;
    }

    private static Connection CreateClientConnection(string directory)
    {
        var connection = new Connection { Id = Guid.NewGuid(), Name = directory, Type = ConnectionType.OpcUaClient };
        connection.SetOpcUaClientConnection(new OpcUaClientConnection
        {
            ApplicationCertificatesPath = $"{directory}/application",
            TrustedCertificatesPath = $"{directory}/trusted",
            TrustedIssuerCertificatesPath = $"{directory}/trusted-issuer",
        });
        return connection;
    }

    [Fact]
    public async Task Should_delete_the_stores_no_OPC_UA_connection_uses()
    {
        // Arrange
        _mediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
            .Returns(new GetConnectionsResponse([CreateClientConnection("ua-certs/0123")], null!));

        // Act
        await RunAsync();

        // Assert
        await _mediator.Received(1).Request<GetConnections, GetConnectionsResponse>(
            Arg.Is<GetConnections>(r => r.FilterTypeNames!.SequenceEqual(new[] { "opcua-client", "opcua-server" })), Arg.Any<CancellationToken>());
        _store.Received(1).CleanupOrphanedStores(
            Arg.Is<IEnumerable<string?>>(paths => paths.SequenceEqual(new[] { "ua-certs/0123/application", "ua-certs/0123/trusted", "ua-certs/0123/trusted-issuer" })),
            TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Should_delete_nothing_if_the_connections_cannot_be_loaded()
    {
        // Arrange
        _mediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
            .Returns(new GetConnectionsResponse([], new ErrorInfo(1, "unavailable")));

        // Act
        await RunAsync();

        // Assert
        _store.DidNotReceiveWithAnyArgs().CleanupOrphanedStores(default!, default);
    }

    [Fact]
    public async Task Should_not_fail_the_module_if_the_request_throws()
    {
        // Arrange
        _mediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
            .Returns<GetConnectionsResponse>(_ => throw new InvalidOperationException("bus down"));

        // Act
        var act = RunAsync;

        // Assert
        await act.Should().NotThrowAsync();
        _store.DidNotReceiveWithAnyArgs().CleanupOrphanedStores(default!, default);
    }
}
