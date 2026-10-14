using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClusterManagement.Backend.Connections;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaCertificateStoreTests
{
    private const string StorePath = "ua-certs/0123/trusted";

    private static readonly string s_homeDirectory = MockUnixSupport.Path(@"c:\home");
    private static readonly string s_cacheDirectory = MockUnixSupport.Path(@"c:\cache");
    private static readonly string s_uploadDirectory = Path.Combine(s_cacheDirectory, OpcUaCertificateUpload.DirectoryName);
    private static readonly string s_storeDirectory = Path.Combine(s_homeDirectory, "ua-certs", "0123", "trusted");
    private static readonly string s_certificatesDirectory = Path.Combine(s_storeDirectory, "certs");

    private readonly MockFileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly OpcUaCertificateStore _store;
    // created once, generating RSA keys is slow
    private static readonly X509Certificate2 s_certificate = CreateCertificate("CN=Machine 1, O=ViciOne");
    private static readonly X509Certificate2 s_otherCertificate = CreateCertificate("CN=Machine 2");

    public OpcUaCertificateStoreTests()
    {
        _workspaceProvider.Home.Returns(s_homeDirectory);
        _workspaceProvider.Cache.Returns(s_cacheDirectory);
        _store = new(_fileSystem, _workspaceProvider, NullLogger<OpcUaCertificateStore>.Instance);
    }

    private static X509Certificate2 CreateCertificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        // without the private key, like a certificate someone hands over
        return X509CertificateLoader.LoadCertificate(certificate.RawData);
    }

    private const string IssuerStorePath = "ua-certs/0123/trusted-issuer";

    private static readonly string s_issuerStoreDirectory = Path.Combine(s_homeDirectory, "ua-certs", "0123", "trusted-issuer");

    // created once; ECDSA keys, which are quick to generate
    private static readonly (X509Certificate2 Root, X509Certificate2 Intermediate, X509Certificate2 Leaf) s_chain = CreateChain();

    private static (X509Certificate2 Root, X509Certificate2 Intermediate, X509Certificate2 Leaf) CreateChain()
    {
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        using var rootKey = ECDsa.Create();
        var rootRequest = new CertificateRequest("CN=Root CA", rootKey, HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(notBefore, notAfter);

        using var intermediateKey = ECDsa.Create();
        var intermediateRequest = new CertificateRequest("CN=Intermediate CA", intermediateKey, HashAlgorithmName.SHA256);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var intermediate = intermediateRequest.Create(root, notBefore, notAfter, [1]);
        using var intermediateWithKey = intermediate.CopyWithPrivateKey(intermediateKey);

        using var leafKey = ECDsa.Create();
        using var leaf = new CertificateRequest("CN=Client 1", leafKey, HashAlgorithmName.SHA256).Create(intermediateWithKey, notBefore, notAfter, [2]);

        return (X509CertificateLoader.LoadCertificate(root.RawData),
            X509CertificateLoader.LoadCertificate(intermediate.RawData),
            X509CertificateLoader.LoadCertificate(leaf.RawData));
    }

    // the order of a fullchain.pem: the leaf first, then its issuers
    private static string ChainPem()
        => string.Join("\n", s_chain.Leaf.ExportCertificatePem(), s_chain.Intermediate.ExportCertificatePem(), s_chain.Root.ExportCertificatePem());

    private const string ApplicationStorePath = "ua-certs/0123/application";
    private const string ApplicationUri = "urn:machine-1:OPCUA:Server";

    private static readonly string s_applicationStoreDirectory = Path.Combine(s_homeDirectory, "ua-certs", "0123", "application");

    // created once, generating RSA keys is slow
    private static readonly X509Certificate2 s_applicationCertificate = CreateApplicationCertificate();

    private static X509Certificate2 CreateApplicationCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Machine 1, DC=machine-1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddUri(new Uri(ApplicationUri));
        alternativeNames.AddDnsName("machine-1");
        request.CertificateExtensions.Add(alternativeNames.Build());

        // with the private key, like an application certificate someone exports
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static string GetStoredFileName(X509Certificate2 certificate)
        => $"{certificate.GetNameInfo(X509NameType.SimpleName, false)} [{certificate.Thumbprint}].der";

    private string AddUpload(string name, byte[] content)
    {
        var path = Path.Combine(s_uploadDirectory, name);
        _fileSystem.AddFile(path, new MockFileData(content));
        return path;
    }

    private void AddStoredCertificate(X509Certificate2 certificate)
        => _fileSystem.AddFile(Path.Combine(s_certificatesDirectory, GetStoredFileName(certificate)), new MockFileData(certificate.RawData));

    public sealed class GetCertificates : OpcUaCertificateStoreTests
    {
        [Fact]
        public void Should_return_no_certificates_for_a_store_that_does_not_exist_yet()
            => _store.GetCertificates(StorePath).Should().BeEmpty();

        [Fact]
        public void Should_return_the_certificates_of_the_store()
        {
            // Arrange
            AddStoredCertificate(s_otherCertificate);
            AddStoredCertificate(s_certificate);

            // Act
            var certificates = _store.GetCertificates(StorePath);

            // Assert
            certificates.Should().Equal(
                new OpcUaCertificateInfo(s_certificate.Thumbprint, "Machine 1", s_certificate.Subject, s_certificate.Issuer,
                    new(s_certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero), new(s_certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero)),
                new OpcUaCertificateInfo(s_otherCertificate.Thumbprint, "Machine 2", s_otherCertificate.Subject, s_otherCertificate.Issuer,
                    new(s_otherCertificate.NotBefore.ToUniversalTime(), TimeSpan.Zero), new(s_otherCertificate.NotAfter.ToUniversalTime(), TimeSpan.Zero)));
        }

        [Fact]
        public void Should_return_the_certificates_of_a_store_path_of_3_0_0_beta2()
        {
            // Arrange
            var store = Path.Combine(s_homeDirectory, "ua-certs", "0123456789abcdef0123456789abcdef", "trusted");
            _fileSystem.AddFile(Path.Combine(store, "certs", GetStoredFileName(s_certificate)), new MockFileData(s_certificate.RawData));

            // Act
            var certificates = _store.GetCertificates(store);

            // Assert
            certificates.Should().ContainSingle().Which.Thumbprint.Should().Be(s_certificate.Thumbprint);
        }

        [Fact]
        public void Should_skip_files_the_OPC_UA_stack_does_not_load()
        {
            // Arrange
            AddStoredCertificate(s_certificate);
            _fileSystem.AddFile(Path.Combine(s_certificatesDirectory, "broken.der"), new MockFileData("no certificate"));
            _fileSystem.AddFile(Path.Combine(s_certificatesDirectory, "other.pem"), new MockFileData(s_otherCertificate.ExportCertificatePem()));

            // Act
            var certificates = _store.GetCertificates(StorePath);

            // Assert
            certificates.Should().ContainSingle().Which.Thumbprint.Should().Be(s_certificate.Thumbprint);
        }

        [Theory]
        [InlineData("other/trusted")]
        [InlineData("ua-certs/../trusted")]
        public void Should_throw_for_an_invalid_store_path(string storePath)
            => FluentActions.Invoking(() => _store.GetCertificates(storePath))
                .Should().Throw<OpcUaCertificateException>().Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidStorePath);
    }

    public sealed class AddCertificate : OpcUaCertificateStoreTests
    {
        [Fact]
        public async Task Should_store_a_DER_certificate_with_the_naming_of_the_OPC_UA_stack()
        {
            // Arrange
            var upload = AddUpload("upload", s_certificate.RawData);

            // Act
            var infos = (await _store.AddCertificates(StorePath, upload, CancellationToken.None)).Certificates;

            // Assert
            var info = infos.Should().ContainSingle().Subject;
            info.Thumbprint.Should().Be(s_certificate.Thumbprint);
            info.Subject.Should().Be(s_certificate.Subject);
            _fileSystem.File.ReadAllBytes(Path.Combine(s_certificatesDirectory, "Machine 1 [" + s_certificate.Thumbprint + "].der"))
                .Should().Equal(s_certificate.RawData);
        }

        [Fact]
        public async Task Should_store_a_PEM_certificate_DER_encoded()
        {
            // Arrange
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(s_certificate.ExportCertificatePem()));

            // Act
            await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            _fileSystem.File.ReadAllBytes(Path.Combine(s_certificatesDirectory, GetStoredFileName(s_certificate)))
                .Should().Equal(s_certificate.RawData);
        }

        [Fact]
        public async Task Should_use_the_certificate_of_a_PEM_file_with_other_sections()
        {
            // Arrange
            using var key = RSA.Create(2048);
            var pem = key.ExportPkcs8PrivateKeyPem() + "\n" + s_certificate.ExportCertificatePem();
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(pem));

            // Act
            var infos = (await _store.AddCertificates(StorePath, upload, CancellationToken.None)).Certificates;

            // Assert
            infos.Should().ContainSingle().Which.Thumbprint.Should().Be(s_certificate.Thumbprint);
        }

        [Fact]
        public async Task Should_store_all_certificates_of_a_PEM_file_none_of_which_issued_another()
        {
            // Arrange
            var pem = s_certificate.ExportCertificatePem() + "\n" + s_otherCertificate.ExportCertificatePem();
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(pem));

            // Act
            var result = await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            result.Certificates.Select(i => i.Thumbprint).Should().Equal(s_certificate.Thumbprint, s_otherCertificate.Thumbprint);
            result.IssuerCertificatesLeftOut.Should().BeFalse();
            _fileSystem.File.ReadAllBytes(Path.Combine(s_certificatesDirectory, GetStoredFileName(s_certificate)))
                .Should().Equal(s_certificate.RawData);
            _fileSystem.File.ReadAllBytes(Path.Combine(s_certificatesDirectory, GetStoredFileName(s_otherCertificate)))
                .Should().Equal(s_otherCertificate.RawData);
        }

        [Fact]
        public async Task Should_store_only_the_leaf_of_a_chain_in_a_trusted_store()
        {
            // Arrange
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(ChainPem()));

            // Act
            var result = await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            result.Certificates.Should().ContainSingle().Which.Thumbprint.Should().Be(s_chain.Leaf.Thumbprint);
            result.IssuerCertificatesLeftOut.Should().BeTrue();
            _fileSystem.Directory.GetFiles(s_certificatesDirectory).Select(Path.GetFileName).Should().Equal(GetStoredFileName(s_chain.Leaf));
        }

        [Fact]
        public async Task Should_store_all_certificates_of_a_chain_in_the_trusted_issuer_store()
        {
            // Arrange
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(ChainPem()));

            // Act
            var result = await _store.AddCertificates(IssuerStorePath, upload, CancellationToken.None);

            // Assert
            result.Certificates.Select(i => i.Thumbprint).Should().Equal(s_chain.Leaf.Thumbprint, s_chain.Intermediate.Thumbprint, s_chain.Root.Thumbprint);
            result.IssuerCertificatesLeftOut.Should().BeFalse();
            _fileSystem.Directory.GetFiles(Path.Combine(s_issuerStoreDirectory, "certs")).Should().HaveCount(3);
        }

        [Fact]
        public async Task Should_store_a_CA_certificate_uploaded_on_its_own_in_a_trusted_store()
        {
            // Arrange
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(s_chain.Intermediate.ExportCertificatePem()));

            // Act
            var result = await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            result.Certificates.Should().ContainSingle().Which.Thumbprint.Should().Be(s_chain.Intermediate.Thumbprint);
            result.IssuerCertificatesLeftOut.Should().BeFalse();
        }

        [Fact]
        public async Task Should_return_a_stored_certificate_of_a_PEM_file_and_store_only_the_others()
        {
            // Arrange
            _fileSystem.AddFile(Path.Combine(s_certificatesDirectory, "renamed.der"), new MockFileData(s_certificate.RawData));
            var pem = s_certificate.ExportCertificatePem() + "\n" + s_otherCertificate.ExportCertificatePem();
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(pem));

            // Act
            var infos = (await _store.AddCertificates(StorePath, upload, CancellationToken.None)).Certificates;

            // Assert
            infos.Select(i => i.Thumbprint).Should().Equal(s_certificate.Thumbprint, s_otherCertificate.Thumbprint);
            _fileSystem.Directory.GetFiles(s_certificatesDirectory).Select(Path.GetFileName)
                .Should().BeEquivalentTo("renamed.der", GetStoredFileName(s_otherCertificate));
        }

        [Fact]
        public async Task Should_return_a_certificate_a_PEM_file_holds_twice_once()
        {
            // Arrange
            var pem = s_certificate.ExportCertificatePem() + "\n" + s_certificate.ExportCertificatePem();
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(pem));

            // Act
            var infos = (await _store.AddCertificates(StorePath, upload, CancellationToken.None)).Certificates;

            // Assert
            infos.Should().ContainSingle();
            _fileSystem.Directory.GetFiles(s_certificatesDirectory).Should().ContainSingle();
        }

        [Fact]
        public async Task Should_store_none_of_the_certificates_of_a_PEM_file_with_an_invalid_one()
        {
            // Arrange
            var pem = s_certificate.ExportCertificatePem() + "\n-----BEGIN CERTIFICATE-----\nbm8gY2VydGlmaWNhdGU=\n-----END CERTIFICATE-----\n";
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(pem));

            // Act
            var act = () => _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidCertificate);
            _fileSystem.Directory.Exists(s_certificatesDirectory).Should().BeFalse();
        }

        [Fact]
        public async Task Should_delete_the_uploaded_file()
        {
            // Arrange
            var upload = AddUpload("upload", s_certificate.RawData);

            // Act
            await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            _fileSystem.File.Exists(upload).Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_store_a_certificate_twice()
        {
            // Arrange
            AddStoredCertificate(s_certificate);
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes(s_certificate.ExportCertificatePem()));

            // Act
            await _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            _fileSystem.Directory.GetFiles(s_certificatesDirectory).Should().ContainSingle();
        }

        [Fact]
        public async Task Should_reject_a_file_that_is_no_certificate_and_delete_it()
        {
            // Arrange
            var upload = AddUpload("upload", System.Text.Encoding.ASCII.GetBytes("no certificate"));

            // Act
            var act = () => _store.AddCertificates(StorePath, upload, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidCertificate);
            _fileSystem.File.Exists(upload).Should().BeFalse();
            _fileSystem.Directory.Exists(s_certificatesDirectory).Should().BeFalse();
        }

        [Fact]
        public async Task Should_reject_a_file_outside_the_upload_directory_and_keep_it()
        {
            // Arrange
            var file = Path.Combine(s_homeDirectory, "certificate.der");
            _fileSystem.AddFile(file, new MockFileData(s_certificate.RawData));

            // Act
            var act = () => _store.AddCertificates(StorePath, file, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.UploadedFileOutsideUploadDirectory);
            _fileSystem.File.Exists(file).Should().BeTrue();
        }

        [Fact]
        public async Task Should_reject_a_missing_upload()
        {
            // Act
            var act = () => _store.AddCertificates(StorePath, Path.Combine(s_uploadDirectory, "missing"), CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.UploadedFileNotFound);
        }

        [Fact]
        public async Task Should_reject_the_application_store()
        {
            // Arrange
            var upload = AddUpload("upload", s_certificate.RawData);

            // Act
            var act = () => _store.AddCertificates("ua-certs/0123/application", upload, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidStorePath);
            _fileSystem.Directory.Exists(Path.Combine(s_applicationStoreDirectory, "certs")).Should().BeFalse();
        }

        [Fact]
        public async Task Should_reject_an_invalid_store_path()
        {
            // Arrange
            var upload = AddUpload("upload", s_certificate.RawData);

            // Act
            var act = () => _store.AddCertificates("ua-certs/../trusted", upload, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidStorePath);
        }
    }

    public sealed class AddApplicationCertificate : OpcUaCertificateStoreTests
    {
        private static string PrivateKeyPem() => s_applicationCertificate.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem();

        private Task<OpcUaApplicationCertificateResult> Add(byte[] content, string? password = null, string subject = "CN=Machine 1, DC=machine-1", string applicationUri = ApplicationUri)
            => _store.AddApplicationCertificate(ApplicationStorePath, AddUpload("upload", content), password, subject, applicationUri, CancellationToken.None);

        private void AssertStoredWithPrivateKey()
        {
            var fileName = GetStoredFileName(s_applicationCertificate);
            _fileSystem.File.ReadAllBytes(Path.Combine(s_applicationStoreDirectory, "certs", fileName)).Should().Equal(s_applicationCertificate.RawData);

            // stored without a password, like the OPC UA stack stores the keys it creates
            using var stored = X509CertificateLoader.LoadPkcs12(
                _fileSystem.File.ReadAllBytes(Path.Combine(s_applicationStoreDirectory, "private", Path.ChangeExtension(fileName, ".pfx"))), null);
            stored.HasPrivateKey.Should().BeTrue();
            stored.Thumbprint.Should().Be(s_applicationCertificate.Thumbprint);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public async Task Should_store_a_PFX_file_with_its_private_key(string? password)
        {
            // Act
            var result = await Add(s_applicationCertificate.Export(X509ContentType.Pkcs12, password), password);

            // Assert
            result.Certificate.Thumbprint.Should().Be(s_applicationCertificate.Thumbprint);
            result.Certificate.ApplicationUri.Should().Be(ApplicationUri);
            result.Warnings.Should().BeEmpty();
            AssertStoredWithPrivateKey();
        }

        [Fact]
        public async Task Should_store_a_PEM_file_with_the_certificate_and_its_private_key()
        {
            // Act
            await Add(System.Text.Encoding.ASCII.GetBytes(s_applicationCertificate.ExportCertificatePem() + "\n" + PrivateKeyPem()));

            // Assert
            AssertStoredWithPrivateKey();
        }

        [Fact]
        public async Task Should_store_a_PEM_file_with_an_encrypted_private_key()
        {
            // Arrange
            var encryptedKey = s_applicationCertificate.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(
                "secret", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));

            // Act
            await Add(System.Text.Encoding.ASCII.GetBytes(s_applicationCertificate.ExportCertificatePem() + "\n" + encryptedKey), "secret");

            // Assert
            AssertStoredWithPrivateKey();
        }

        [Fact]
        public async Task Should_replace_the_certificate_in_the_store()
        {
            // Arrange
            var oldCertificate = Path.Combine(s_applicationStoreDirectory, "certs", "OPC-UA Server [0A1B].der");
            var oldPrivateKey = Path.Combine(s_applicationStoreDirectory, "private", "OPC-UA Server [0A1B].pfx");
            _fileSystem.AddFile(oldCertificate, new MockFileData("old"));
            _fileSystem.AddFile(oldPrivateKey, new MockFileData("old"));

            // Act
            await Add(s_applicationCertificate.Export(X509ContentType.Pkcs12));

            // Assert
            _fileSystem.File.Exists(oldCertificate).Should().BeFalse();
            _fileSystem.File.Exists(oldPrivateKey).Should().BeFalse();
            AssertStoredWithPrivateKey();
        }

        [Fact]
        public async Task Should_warn_if_the_certificate_does_not_match_the_settings_of_the_connection()
        {
            // Act
            var result = await Add(s_applicationCertificate.Export(X509ContentType.Pkcs12), subject: "CN=OPC-UA Server", applicationUri: "urn:other");

            // Assert
            result.Warnings.Should().Equal(OpcUaApplicationCertificateWarning.SubjectMismatch, OpcUaApplicationCertificateWarning.ApplicationUriMismatch);
            AssertStoredWithPrivateKey();
        }

        [Fact]
        public async Task Should_reject_a_store_of_trusted_certificates_and_keep_its_certificates()
        {
            // Arrange
            AddStoredCertificate(s_certificate);

            // Act
            var act = () => _store.AddApplicationCertificate(StorePath, AddUpload("upload", s_applicationCertificate.Export(X509ContentType.Pkcs12)),
                null, "CN=Machine 1", ApplicationUri, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidStorePath);
            _store.GetCertificates(StorePath).Should().ContainSingle().Which.Thumbprint.Should().Be(s_certificate.Thumbprint);
        }

        [Fact]
        public async Task Should_keep_the_certificate_when_the_same_one_is_uploaded_again()
        {
            // Arrange
            await Add(s_applicationCertificate.Export(X509ContentType.Pkcs12));

            // Act
            await Add(s_applicationCertificate.Export(X509ContentType.Pkcs12));

            // Assert
            AssertStoredWithPrivateKey();
            _fileSystem.Directory.GetFiles(Path.Combine(s_applicationStoreDirectory, "certs")).Should().ContainSingle();
            _fileSystem.Directory.GetFiles(Path.Combine(s_applicationStoreDirectory, "private")).Should().ContainSingle();
        }

        [Fact]
        public async Task Should_reject_a_wrong_password_and_keep_the_current_certificate()
        {
            // Arrange
            var current = Path.Combine(s_applicationStoreDirectory, "certs", "OPC-UA Server [0A1B].der");
            _fileSystem.AddFile(current, new MockFileData("current"));

            // Act
            var act = () => Add(s_applicationCertificate.Export(X509ContentType.Pkcs12, "secret"), "wrong");

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidApplicationCertificate);
            _fileSystem.File.Exists(current).Should().BeTrue();
        }

        [Fact]
        public async Task Should_reject_a_PEM_file_without_private_key()
        {
            // Act
            var act = () => Add(System.Text.Encoding.ASCII.GetBytes(s_applicationCertificate.ExportCertificatePem()));

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.NoPrivateKey);
        }

        [Fact]
        public async Task Should_reject_a_DER_certificate_without_private_key()
        {
            // Act
            var act = () => Add(s_applicationCertificate.RawData);

            // Assert
            (await act.Should().ThrowAsync<OpcUaCertificateException>()).Which.ErrorCode.Should().Be(OpcUaCertificateError.InvalidApplicationCertificate);
        }

        [Fact]
        public async Task Should_delete_the_uploaded_file()
        {
            // Arrange
            var upload = AddUpload("upload", s_applicationCertificate.Export(X509ContentType.Pkcs12));

            // Act
            await _store.AddApplicationCertificate(ApplicationStorePath, upload, null, "CN=Machine 1", ApplicationUri, CancellationToken.None);

            // Assert
            _fileSystem.File.Exists(upload).Should().BeFalse();
        }
    }

    public sealed class DeleteCertificate : OpcUaCertificateStoreTests
    {
        [Fact]
        public void Should_delete_the_certificate_and_its_private_key()
        {
            // Arrange
            AddStoredCertificate(s_certificate);
            AddStoredCertificate(s_otherCertificate);
            var privateKey = Path.Combine(s_storeDirectory, "private", Path.ChangeExtension(GetStoredFileName(s_certificate), ".pfx"));
            var otherPrivateKey = Path.Combine(s_storeDirectory, "private", Path.ChangeExtension(GetStoredFileName(s_otherCertificate), ".pfx"));
            _fileSystem.AddFile(privateKey, new MockFileData("key"));
            _fileSystem.AddFile(otherPrivateKey, new MockFileData("key"));

            // Act
            _store.DeleteCertificate(StorePath, s_certificate.Thumbprint);

            // Assert
            _store.GetCertificates(StorePath).Should().ContainSingle().Which.Thumbprint.Should().Be(s_otherCertificate.Thumbprint);
            _fileSystem.File.Exists(privateKey).Should().BeFalse();
            _fileSystem.File.Exists(otherPrivateKey).Should().BeTrue();
        }

        [Fact]
        public void Should_find_the_certificate_by_the_thumbprint_in_its_file_name_without_reading_it()
        {
            // Arrange
            var file = Path.Combine(s_certificatesDirectory, $"Machine 1 [{s_certificate.Thumbprint}].der");
            _fileSystem.AddFile(file, new MockFileData("not parsed"));

            // Act
            _store.DeleteCertificate(StorePath, s_certificate.Thumbprint);

            // Assert
            _fileSystem.File.Exists(file).Should().BeFalse();
        }

        [Fact]
        public void Should_delete_a_certificate_stored_under_another_name()
        {
            // Arrange
            var file = Path.Combine(s_certificatesDirectory, "copied by hand.der");
            _fileSystem.AddFile(file, new MockFileData(s_certificate.RawData));

            // Act
            _store.DeleteCertificate(StorePath, s_certificate.Thumbprint);

            // Assert
            _fileSystem.File.Exists(file).Should().BeFalse();
        }

        [Fact]
        public void Should_throw_if_the_store_has_no_such_certificate()
        {
            // Arrange
            AddStoredCertificate(s_otherCertificate);

            // Act & Assert
            FluentActions.Invoking(() => _store.DeleteCertificate(StorePath, s_certificate.Thumbprint))
                .Should().Throw<OpcUaCertificateException>().Which.ErrorCode.Should().Be(OpcUaCertificateError.CertificateNotFound);
        }
    }

    public sealed class DeleteConnectionStores : OpcUaCertificateStoreTests
    {
        [Fact]
        public void Should_delete_the_directory_of_the_connection_with_all_its_stores()
        {
            // Arrange
            AddStoredCertificate(s_certificate);
            var applicationKey = Path.Combine(s_homeDirectory, "ua-certs", "0123", "application", "private", "key.pfx");
            var otherConnection = Path.Combine(s_homeDirectory, "ua-certs", "4567", "trusted", "certs", "other.der");
            _fileSystem.AddFile(applicationKey, new MockFileData("key"));
            _fileSystem.AddFile(otherConnection, new MockFileData("other"));

            // Act
            _store.DeleteConnectionStores(["ua-certs/0123/application", StorePath, "ua-certs/0123/trusted-issuer"]);

            // Assert
            _fileSystem.Directory.Exists(Path.Combine(s_homeDirectory, "ua-certs", "0123")).Should().BeFalse();
            _fileSystem.File.Exists(otherConnection).Should().BeTrue();
        }

        [Fact]
        public void Should_ignore_paths_that_are_no_store_paths_and_stores_that_do_not_exist()
        {
            // Arrange
            var file = Path.Combine(s_homeDirectory, "other", "file");
            _fileSystem.AddFile(file, new MockFileData("other"));

            // Act
            _store.DeleteConnectionStores([null, "", "other", Path.Combine(s_homeDirectory, "other"), "ua-certs/../other", "ua-certs/missing/trusted"]);

            // Assert
            _fileSystem.File.Exists(file).Should().BeTrue();
        }
    }

    public sealed class CleanupOrphanedStores : OpcUaCertificateStoreTests
    {
        private static readonly DateTime s_old = DateTime.UtcNow - TimeSpan.FromDays(2);

        private string AddConnectionDirectory(string name, DateTime lastWriteTime)
        {
            var directory = Path.Combine(s_homeDirectory, "ua-certs", name);
            var file = Path.Combine(directory, "trusted", "certs", "certificate.der");
            _fileSystem.AddFile(file, new MockFileData("certificate"));
            foreach (var path in new[] { directory, Path.Combine(directory, "trusted"), Path.Combine(directory, "trusted", "certs") })
                _fileSystem.Directory.SetLastWriteTimeUtc(path, s_old);
            _fileSystem.File.SetLastWriteTimeUtc(file, lastWriteTime);
            return directory;
        }

        [Fact]
        public void Should_delete_the_directories_no_connection_uses()
        {
            // Arrange
            var unused = AddConnectionDirectory("unused", s_old);
            var used = AddConnectionDirectory("used", s_old);

            // Act
            _store.CleanupOrphanedStores(["ua-certs/used/application", "ua-certs/used/trusted", null], TimeSpan.FromMinutes(30));

            // Assert
            _fileSystem.Directory.Exists(unused).Should().BeFalse();
            _fileSystem.Directory.Exists(used).Should().BeTrue();
        }

        [Fact]
        public void Should_keep_the_directory_a_connection_of_3_0_0_beta2_uses()
        {
            // Arrange
            var used = AddConnectionDirectory("0123456789abcdef0123456789abcdef", s_old);

            // Act
            _store.CleanupOrphanedStores([Path.Combine(s_homeDirectory, "ua-certs", "0123456789abcdef0123456789abcdef", "trusted")], TimeSpan.FromMinutes(30));

            // Assert
            _fileSystem.Directory.Exists(used).Should().BeTrue();
        }

        [Fact]
        public void Should_keep_a_directory_with_a_recent_change_deep_inside()
        {
            // Arrange
            var recent = AddConnectionDirectory("recent", DateTime.UtcNow);

            // Act
            _store.CleanupOrphanedStores([], TimeSpan.FromMinutes(30));

            // Assert
            _fileSystem.Directory.Exists(recent).Should().BeTrue();
        }

        [Fact]
        public void Should_do_nothing_without_certificates_directory()
            => FluentActions.Invoking(() => _store.CleanupOrphanedStores([], TimeSpan.FromMinutes(30))).Should().NotThrow();
    }

    public sealed class CleanupUploadLeftovers : OpcUaCertificateStoreTests
    {
        [Fact]
        public void Should_delete_stale_uploads_and_keep_recent_ones()
        {
            // Arrange
            var staleUpload = AddUpload("stale", s_certificate.RawData);
            var recentUpload = AddUpload("recent", s_certificate.RawData);
            _fileSystem.File.SetLastWriteTimeUtc(staleUpload, DateTime.UtcNow - TimeSpan.FromHours(1));

            // Act
            _store.CleanupUploadLeftovers(TimeSpan.FromMinutes(5));

            // Assert
            _fileSystem.File.Exists(staleUpload).Should().BeFalse();
            _fileSystem.File.Exists(recentUpload).Should().BeTrue();
        }
    }
}
