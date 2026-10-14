using ClusterManagement.Backend.Connections;
using ClusterManagement.Backend.Connections.Consumers;
using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Connections.Consumers;

public class OpcUaConnectionDeletedConsumerTests
{
    private readonly IOpcUaCertificateStore _store = Substitute.For<IOpcUaCertificateStore>();
    private readonly OpcUaConnectionDeletedConsumer _consumer;

    public OpcUaConnectionDeletedConsumerTests() => _consumer = new(_store, NullLogger<OpcUaConnectionDeletedConsumer>.Instance);

    private static Connection CreateServerConnection()
    {
        var connection = new Connection { Id = Guid.NewGuid(), Name = "OPC/UA", Type = ConnectionType.OpcUaServer };
        connection.SetOpcUaServerConnection(new OpcUaServerConnection
        {
            ApplicationCertificatesPath = "ua-certs/0123/application",
            TrustedCertificatesPath = "ua-certs/0123/trusted",
            TrustedIssuerCertificatesPath = "ua-certs/0123/trusted-issuer",
        });
        return connection;
    }

    private Task Consume(CrudAction action, Connection connection)
    {
        var context = Substitute.For<ConsumeContext<ConnectionChanged>>();
        context.Message.Returns(new ConnectionChanged(action, connection, [], []) { CorrelationId = Guid.NewGuid() });
        return _consumer.Consume(context);
    }

    [Fact]
    public async Task Should_delete_the_stores_of_a_deleted_OPC_UA_connection()
    {
        // Act
        await Consume(CrudAction.Deleted, CreateServerConnection());

        // Assert
        _store.Received(1).DeleteConnectionStores(Arg.Is<IEnumerable<string?>>(paths =>
            paths.SequenceEqual(new[] { "ua-certs/0123/application", "ua-certs/0123/trusted", "ua-certs/0123/trusted-issuer" })));
    }

    [Theory]
    [InlineData(CrudAction.Created)]
    [InlineData(CrudAction.Updated)]
    public async Task Should_keep_the_stores_of_a_connection_that_is_not_deleted(CrudAction action)
    {
        // Act
        await Consume(action, CreateServerConnection());

        // Assert
        _store.DidNotReceiveWithAnyArgs().DeleteConnectionStores(default!);
    }

    [Fact]
    public async Task Should_leave_stores_it_cannot_delete_to_the_cleanup_instead_of_failing()
    {
        // Arrange
        _store.When(s => s.DeleteConnectionStores(Arg.Any<IEnumerable<string?>>())).Throw(new IOException("in use"));

        // Act
        var act = () => Consume(CrudAction.Deleted, CreateServerConnection());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_delete_no_stores_of_another_connection_type()
    {
        // Act
        await Consume(CrudAction.Deleted, new Connection { Id = Guid.NewGuid(), Name = "MQTT", Type = ConnectionType.Mqtt });

        // Assert
        _store.Received(1).DeleteConnectionStores(Arg.Is<IEnumerable<string?>>(paths => !paths.Any()));
    }
}
