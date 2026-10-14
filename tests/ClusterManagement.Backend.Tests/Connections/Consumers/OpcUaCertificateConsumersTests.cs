using ClusterManagement.Backend.Connections;
using ClusterManagement.Backend.Connections.Consumers;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Connections.Consumers;

public class OpcUaCertificateConsumersTests
{
    private const string StorePath = "ua-certs/0123/trusted";

    private static readonly OpcUaCertificateInfo s_certificate = new("0A1B", "Machine", "CN=Machine", "CN=Machine", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1));

    private readonly IOpcUaCertificateStore _store = Substitute.For<IOpcUaCertificateStore>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public OpcUaCertificateConsumersTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetOpcUaCertificatesConsumer>();
            cfg.AddConsumer<AddOpcUaCertificateConsumer>();
            cfg.AddConsumer<DeleteOpcUaCertificateConsumer>();
            cfg.AddSingleton(_store);
            cfg.AddSingleton(Substitute.For<ILogger<GetOpcUaCertificatesConsumer>>());
            cfg.AddSingleton(Substitute.For<ILogger<AddOpcUaCertificateConsumer>>());
            cfg.AddSingleton(Substitute.For<ILogger<DeleteOpcUaCertificateConsumer>>());
        };

    public sealed class GetOpcUaCertificates : OpcUaCertificateConsumersTests
    {
        [Fact]
        public async Task Should_respond_with_the_certificates_of_the_store()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _store.GetCertificates(StorePath).Returns([s_certificate]);

            // Act
            var response = await tester.TestRequest<GetOpcUaCertificatesResponse, GetOpcUaCertificatesRequest>(new(StorePath));

            // Assert
            response.RequestError.Should().BeNull();
            response.Certificates.Should().Equal(s_certificate);
        }

        [Fact]
        public async Task Should_respond_with_the_error_code_of_a_failure()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _store.GetCertificates(StorePath).Throws(new OpcUaCertificateException(OpcUaCertificateError.InvalidStorePath, "invalid"));

            // Act
            var response = await tester.TestRequest<GetOpcUaCertificatesResponse, GetOpcUaCertificatesRequest>(new(StorePath));

            // Assert
            response.Certificates.Should().BeEmpty();
            response.RequestError!.ErrorCode.Should().Be(OpcUaCertificateError.InvalidStorePath);
        }
    }

    public sealed class AddOpcUaCertificate : OpcUaCertificateConsumersTests
    {
        [Fact]
        public async Task Should_respond_with_the_added_certificates()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _store.AddCertificates(StorePath, "upload", Arg.Any<CancellationToken>()).Returns(new OpcUaCertificatesResult([s_certificate], true));

            // Act
            var response = await tester.TestRequest<AddOpcUaCertificateResponse, AddOpcUaCertificateRequest>(new(StorePath, "upload"));

            // Assert
            response.RequestError.Should().BeNull();
            response.Certificates.Should().Equal(s_certificate);
            response.IssuerCertificatesLeftOut.Should().BeTrue();
        }

        [Fact]
        public async Task Should_respond_with_the_error_code_of_a_failure()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _store.AddCertificates(StorePath, "upload", Arg.Any<CancellationToken>())
                .ThrowsAsync(new OpcUaCertificateException(OpcUaCertificateError.InvalidCertificate, "invalid"));

            // Act
            var response = await tester.TestRequest<AddOpcUaCertificateResponse, AddOpcUaCertificateRequest>(new(StorePath, "upload"));

            // Assert
            response.Certificates.Should().BeEmpty();
            response.RequestError!.ErrorCode.Should().Be(OpcUaCertificateError.InvalidCertificate);
        }
    }

    public sealed class DeleteOpcUaCertificate : OpcUaCertificateConsumersTests
    {
        [Fact]
        public async Task Should_delete_the_certificate()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);

            // Act
            var response = await tester.TestRequest<DeleteOpcUaCertificateResponse, DeleteOpcUaCertificateRequest>(new(StorePath, "0A1B"));

            // Assert
            response.RequestError.Should().BeNull();
            _store.Received(1).DeleteCertificate(StorePath, "0A1B");
        }

        [Fact]
        public async Task Should_respond_with_an_unknown_error_for_an_unexpected_failure()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _store.When(s => s.DeleteCertificate(StorePath, "0A1B")).Throw(new IOException("in use"));

            // Act
            var response = await tester.TestRequest<DeleteOpcUaCertificateResponse, DeleteOpcUaCertificateRequest>(new(StorePath, "0A1B"));

            // Assert
            response.RequestError!.ErrorCode.Should().Be(OpcUaCertificateError.UnknownError);
            response.RequestError.Message.Should().Be("in use");
        }
    }
}
