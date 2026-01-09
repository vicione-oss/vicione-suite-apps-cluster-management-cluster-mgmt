using System.Buffers;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.EngineHosts.CommunicationContracts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Migrations;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Backend.Tests.TestUtils;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Semver;
using Xunit;

namespace ClusterManagement.Backend.Tests.EngineHosts;

public class MqttEngineHostCommunicationFacts
{
    private readonly IMqttRpcClient _mqttClient;
    private readonly IEngineHostPoolCommunication _poolCommunication;
    private readonly MqttEngineHostCommunication _communication;
    private readonly Guid _engineHostId = Guid.NewGuid();
    private readonly Guid _deploymentId = Guid.NewGuid();

    private readonly SemVersion _latestSupportedEngineHostVersion = new(0, 30, 0);
    private readonly SemVersion _oldestSupportedEngineHostVersion = new(0, 30, 0);

    private readonly SemVersion _latestSupportedManagedEngineVersion = new(0, 62, 0);
    private readonly SemVersion _oldestSupportedManagedEngineVersion = new(0, 62, 0);

    public MqttEngineHostCommunicationFacts()
    {
        _mqttClient = Substitute.For<IMqttRpcClient>();
        _poolCommunication = Substitute.For<IEngineHostPoolCommunication>();
        _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_latestSupportedManagedEngineVersion);
        _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_latestSupportedEngineHostVersion);
        _communication = new(_mqttClient, _poolCommunication);
    }

    private static ReadOnlySequence<byte> SerializeJsonToGzipBytes<T>(T instance)
    {
        using MemoryStream memory = new();
        using (GZipStream gzip = new(memory, CompressionMode.Compress))
            JsonSerializer.Serialize(gzip, instance);
        return new(memory.ToArray());
    }

    private static ReadOnlySequence<byte> SerializeJsonToBytes<T>(T instance)
    {
        using MemoryStream memory = new();
        JsonSerializer.Serialize(memory, instance);
        memory.Flush();
        return new(memory.ToArray());
    }

    private static ReadOnlySequence<byte> PlainTextToBytes(string text)
        => new(Encoding.UTF8.GetBytes(text));

    private static ReadOnlySequence<byte> PlainTextToGzipBytes(string text)
    {
        using MemoryStream memory = new();
        using GZipStream gzip = new(memory, CompressionMode.Compress);
        using (StreamWriter writer = new(gzip))
            writer.Write(text);
        return new(memory.ToArray());
    }

    private static string? ReadStringFromGzipBytes(ReadOnlySequence<byte>? bytes)
    {
        if (!bytes.HasValue)
            return default;
        using MemoryStream memory = new(bytes.Value.ToArray());
        using GZipStream gzip = new(memory, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);
        return reader.ReadToEnd();
    }

    private static JsonObject? ReadJsonObjectFromGzipBytes(ReadOnlySequence<byte>? bytes)
    {
        if (!bytes.HasValue)
            return default;
        using MemoryStream memory = new(bytes.Value.ToArray());
        using GZipStream gzip = new(memory, CompressionMode.Decompress);
        var node = JsonNode.Parse(gzip);
        if (node is not null)
            return node.AsObject();
        return default;
    }

    public class DeployEngine : MqttEngineHostCommunicationFacts
    {
        public class General : DeployEngine
        {
            [Fact]
            public async Task Sends_request_with_correct_topics_and_data()
            {
                _mqttClient.Request(default!, default!, default!, default)
                    .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                await _mqttClient.Received(1).Request(
                    Arg.Is($"{_engineHostId}/request"),
                    FluentArg.Assert<MqttRpcRequest>(r =>
                    {
                        r.Method.Should().Be("deploy");
                        r.Payload.HasValue.Should().BeTrue();
                        r.Payload!.Value.IsEmpty.Should().BeFalse();
                        r.Payload.Value.Length.Should().BeInRange(790L, 804L);
                        r.ContentType.Should().Be(MediaTypeNames.Application.GZip);
                    })!,
                    Arg.Is($"{_engineHostId}/response"), default);
            }

            [Fact]
            public async Task Throws_InvalidOperationException_if_response_indicates_failure()
            {
                _mqttClient.Request(default!, default!, default!, default)
                    .ReturnsForAnyArgs(new MqttRpcResponse("fail", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

                var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None));

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*deploy*not possible*");
            }

            [Fact]
            public async Task Returns_received_deployment_identifier()
            {
                _mqttClient.Request(default!, default!, default!, default)
                    .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                var actual = await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                actual.Should().Be(Guid.Empty);
            }

            [Fact]
            public async Task Throws_InvalidOperationException_if_response_is_not_a_GUID()
            {
                _mqttClient.Request(default!, default!, default!, default)
                    .ReturnsForAnyArgs(new MqttRpcResponse("ok", ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.GZip));

                var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None));

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*MQTT*response*'<gzip compressed content>'*");
            }
        }

        public class Migrations : DeployEngine
        {
            private static SemVersion ChangeVersion(SemVersion version, int minorInterval)
            {
                if (version.Minor - minorInterval < 0)
                {
                    if (version.Major == 0)
                        throw new InvalidOperationException("A major version smaller than 0 is not allowed.");
                    version = version.With(version.Major - 1, 0);
                }
                else
                {
                    version = version.WithMinor(minor: version.Minor + minorInterval);
                }
                return version;
            }

            public class ManagedEngine : Migrations
            {
                [Fact]
                public async Task Throws_InvalidOperationException_if_get_managed_engine_version_fails()
                {
                    var underlyingException = new IOException("Ein allgemeiner Fehler.");
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).ThrowsAsync(underlyingException);

                    var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), default));

                    var exeception = await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not*get*version*managed*engine*");
                    exeception.Which.InnerException.Should().BeSameAs(underlyingException);
                }

                [Fact]
                public async Task Throws_OperationCanceledException_if_cancelled_while_getting_managed_engine_version()
                {
                    using CancellationTokenSource cancellationTokenSource = new();
                    _poolCommunication
                        .WhenForAnyArgs(c => c.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)))
                        .Do(_ =>
                        {
                            cancellationTokenSource.Cancel();
                            throw new OperationCanceledException();
                        });

                    var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), cancellationTokenSource.Token));

                    await act.Should().ThrowAsync<OperationCanceledException>();
                    _poolCommunication.ReceivedCalls().Should().NotBeEmpty();
                    _mqttClient.ReceivedCalls().Should().BeEmpty();
                }

                [Fact]
                public async Task Throws_InvalidOperationException_if_one_migration_fails()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(new SemVersion(1, 0, 0));
                    List<IMigration<IStartParameter>> migrations = [];
                    var firstMigration = Substitute.For<IMigration<IStartParameter>>();
                    firstMigration.Version.Returns(new SemVersion(1, 1));
                    firstMigration.Execute(default!).ReturnsForAnyArgs(Substitute.For<IStartParameter>());
                    migrations.Add(firstMigration);
                    var secondMigration = Substitute.For<IMigration<IStartParameter>>();
                    secondMigration.Version.Returns(new SemVersion(1));
                    var exception = new InvalidOperationException("Migration nicht möglich!", new InvalidOperationException("Funktion wird nicht unterstützt."));
                    secondMigration.Execute(default!).ThrowsForAnyArgs(exception);
                    migrations.Add(secondMigration);
                    MqttEngineHostCommunication communication = new(_mqttClient, _poolCommunication, [], migrations);

                    var act = communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None));

                    (await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not*migrate*StartParameter*to*1.0.0*"))
                        .Which.InnerException.Should().BeSameAs(exception);
                }

                [Fact]
                public async Task Does_not_migrate_to_upcoming_version()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_latestSupportedManagedEngineVersion, +1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, CreateMinimalStartParameter()))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Does_not_migrate_to_latest_version()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_latestSupportedManagedEngineVersion);
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, CreateMinimalStartParameter()))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_to_older_version()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_latestSupportedManagedEngineVersion, -1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, CreateMinimalStartParameter()))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_to_oldest_supported_version()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_oldestSupportedManagedEngineVersion);
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, CreateMinimalStartParameter()))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_until_oldest_supported_version_for_outdated_version()
                {
                    _poolCommunication.GetManagedEngineVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_oldestSupportedManagedEngineVersion, -1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, CreateMinimalStartParameter()))!,
                        Arg.Any<string>(), default);
                }

                private static Backend.Engines.CommunicationContracts.StartParameter CreateMinimalStartParameter()
                {
                    Backend.Engines.CommunicationContracts.StartParameter startParameter = new();
                    Backend.Engines.CommunicationContracts.CommunicationConfiguration mqttCommunication = new()
                    {
                        Id = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A",
                        Properties =
                        [
                            new("Host") { Value = string.Empty, },
                            new("Port") { Value = 1, },
                            new("Username"),
                            new("Password"),
                            new("MaxPendingMessages") {Value = 10_000, },
                            new("QualityOfService") { Value = (short)0, },
                            new("Retain") { Value = false, },
                        ],
                    };
                    startParameter.DataDistribution.SettingsPersistenceCommunications.Add(new()
                    {
                        Communication = new()
                        {
                            Id = "8B58C1F9-D120-42E9-8F63-B7C62FE1C415",
                            Properties =
                            [
                                new("Directory") { Value = Path.Combine("persistence", "settings", Guid.Empty.ToString()), },
                            ],
                        },
                        IntervalCycles = 1,
                    });
                    startParameter.DataDistribution.SettingsPersistenceCommunications.Add(new()
                    {
                        Communication = mqttCommunication,
                        IntervalCycles = 1,
                    });
                    startParameter.DataDistribution.VariablesPersistenceCommunications.Add(new()
                    {
                        Communication = new()
                        {
                            Id = "8B58C1F9-D120-42E9-8F63-B7C62FE1C415",
                            Properties =
                            [
                                new("Directory") { Value = Path.Combine("persistence", "variables", Guid.Empty.ToString()), },
                            ],
                        },
                        IntervalCycles = 2147483647,
                    });
                    startParameter.DataDistribution.VariablesPersistenceCommunications.Add(new()
                    {
                        Communication = mqttCommunication,
                        IntervalCycles = 2147483647,
                    });
                    startParameter.DataDistribution.RuntimeFunctionCommunications.Add(new()
                    {
                        Id = "07AB9DE8-6216-41FE-9E94-D324C5C55F7A",
                        Properties =
                        [
                            new("Host") { Value = string.Empty, },
                            new("Port") { Value = 1, },
                            new("Username"),
                            new("Password"),
                            new("MaxPendingMessages") {Value = 10_000, },
                            new("QualityOfService") { Value = (short)0, },
                            new("Retain") { Value = false, },
                        ],
                    });
                    startParameter.Engine.UniqueIdentifier = Guid.Empty.ToString();
                    return startParameter;
                }

                private static void ShouldBe<T>(ReadOnlySequence<byte>? payload, T expectedStartParameter)
                    where T : IStartParameter
                {
                    var jsonObject = ReadJsonObjectFromGzipBytes(payload);
                    jsonObject.Should().NotBeNull();
                    jsonObject![nameof(DeployRequest.StartParameter)].Deserialize<T>(JsonSetup.PreserveTypeOptions).Should().BeEquivalentTo(expectedStartParameter);
                }
            }

            public class EngineHost : Migrations
            {
                [Fact]
                public async Task Throws_InvalidOperationException_if_get_engine_host_version_fails()
                {
                    var underlyingException = new IOException("Ein allgemeiner Fehler.");
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).ThrowsAsync(underlyingException);

                    var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), default));

                    var exeception = await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not*get*version*engine*host*");
                    exeception.Which.InnerException.Should().BeSameAs(underlyingException);
                }

                [Fact]
                public async Task Throws_OperationCanceledException_if_cancelled_while_getting_engine_host_version()
                {
                    using CancellationTokenSource cancellationTokenSource = new();
                    _poolCommunication
                        .WhenForAnyArgs(c => c.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)))
                        .Do(_ =>
                        {
                            cancellationTokenSource.Cancel();
                            throw new OperationCanceledException();
                        });

                    var act = _communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), cancellationTokenSource.Token));

                    await act.Should().ThrowAsync<OperationCanceledException>();
                    _poolCommunication.ReceivedCalls().Should().NotBeEmpty();
                    _mqttClient.ReceivedCalls().Should().BeEmpty();
                }

                [Fact]
                public async Task Throws_InvalidOperationException_if_one_migration_fails()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(new SemVersion(1, 0, 0));
                    List<IMigration<IDeployRequest>> migrations = [];
                    var firstMigration = Substitute.For<IMigration<IDeployRequest>>();
                    firstMigration.Version.Returns(new SemVersion(1, 1));
                    firstMigration.Execute(default!).ReturnsForAnyArgs(Substitute.For<IDeployRequest>());
                    migrations.Add(firstMigration);
                    var secondMigration = Substitute.For<IMigration<IDeployRequest>>();
                    secondMigration.Version.Returns(new SemVersion(1));
                    var exception = new InvalidOperationException("Migration nicht möglich!", new InvalidOperationException("Funktion wird nicht unterstützt."));
                    secondMigration.Execute(default!).ThrowsForAnyArgs(exception);
                    migrations.Add(secondMigration);
                    MqttEngineHostCommunication communication = new(_mqttClient, _poolCommunication, migrations, []);

                    var act = communication.Awaiting(_ => _.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None));

                    (await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not*migrate*DeployRequest*to*1.0.0*"))
                        .Which.InnerException.Should().BeSameAs(exception);
                }

                [Fact]
                public async Task Does_not_migrate_to_upcoming_version()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_latestSupportedEngineHostVersion, +1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, new DeployRequest()
                        {
                            StartParameter = default!,
                            CycleTime = 0,
                            EngineChainIndex = 0,
                            PackageReferences = [],
                        }, nameof(DeployRequest.StartParameter)))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Does_not_migrate_to_latest_version()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_latestSupportedEngineHostVersion);
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, new DeployRequest()
                        {
                            StartParameter = default!,
                            CycleTime = 0,
                            EngineChainIndex = 0,
                            PackageReferences = [],
                        }, nameof(DeployRequest.StartParameter)))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_to_older_version()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_latestSupportedEngineHostVersion, -1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, new DeployRequest()
                        {
                            StartParameter = default!,
                            CycleTime = 0,
                            EngineChainIndex = 0,
                            PackageReferences = [],
                        }, nameof(DeployRequest.StartParameter)))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_to_oldest_supported_version()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(_oldestSupportedEngineHostVersion);
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, new DeployRequest()
                        {
                            StartParameter = default!,
                            CycleTime = 0,
                            EngineChainIndex = 0,
                            PackageReferences = [],
                        }, nameof(DeployRequest.StartParameter)))!,
                        Arg.Any<string>(), default);
                }

                [Fact]
                public async Task Migrates_until_oldest_supported_version_for_outdated_version()
                {
                    _poolCommunication.GetEngineHostVersion(Arg.Any<CancellationToken>(), Arg.Is(_engineHostId)).Returns(ChangeVersion(_oldestSupportedEngineHostVersion, -1));
                    _mqttClient.Request(default!, default!, default!, default)
                        .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(Guid.Empty), MediaTypeNames.Application.GZip));

                    await _communication.DeployEngine(_engineHostId, CreateMinimalWorkflow(), CancellationToken.None);

                    await _mqttClient.Received(1).Request(
                        Arg.Any<string>(),
                        FluentArg.Assert<MqttRpcRequest>(r => ShouldBe(r.Payload, new DeployRequest()
                        {
                            StartParameter = default!,
                            CycleTime = 0,
                            EngineChainIndex = 0,
                            PackageReferences = [],
                        }, nameof(DeployRequest.StartParameter)))!,
                        Arg.Any<string>(), default);
                }

                private static void ShouldBe<T>(ReadOnlySequence<byte>? payload, T expectedDeployRequest, params string[] propertiesToReset)
                    where T : IDeployRequest
                {
                    var jsonObject = ReadJsonObjectFromGzipBytes(payload);
                    jsonObject.Should().NotBeNull();
                    foreach (var propertyToIgnore in propertiesToReset)
                        jsonObject![propertyToIgnore] = default;
                    jsonObject.Deserialize<T>(JsonSetup.DefaultOptions).Should().BeEquivalentTo(expectedDeployRequest);
                }
            }
        }

        private static Workflow CreateMinimalWorkflow()
        {
            Workflow workflow = new();
            workflow.PersistenceCommunication.TcpPort = 1;
            workflow.RuntimeFunctionsCommunication.TcpPort = 1;
            return workflow;
        }
    }

    public class TearDownEngine : MqttEngineHostCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok"));

            await _communication.TearDownEngine(_engineHostId, _deploymentId, CancellationToken.None);

            await _mqttClient.Received(1).Request(
                Arg.Is($"{_engineHostId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("teardown");
                    JsonSerializer.Deserialize<Guid>(r.Payload!.Value.FirstSpan).Should().Be(_deploymentId);
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineHostId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("failed", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.TearDownEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Could not execute*teardown*not possible*");
        }
    }

    public class StartEngine : MqttEngineHostCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes(true), MediaTypeNames.Application.Json));

            await _communication.StartEngine(_engineHostId, _deploymentId, CancellationToken.None);

            await _mqttClient.Received(1).Request(
                Arg.Is($"{_engineHostId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("start");
                    JsonSerializer.Deserialize<Guid>(r.Payload!.Value.FirstSpan).Should().Be(_deploymentId);
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineHostId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.StartEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*start*not possible*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_empty()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.StartEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*''*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_not_parseable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToBytes("asdf"), MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.StartEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'asdf'*");
        }

        [Fact]
        public async Task Returns_received_result()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes(true), MediaTypeNames.Application.Json));

            var actual = await _communication.StartEngine(_engineHostId, Guid.Empty, CancellationToken.None);

            actual.Should().BeTrue();
        }
    }

    public class StopEngine : MqttEngineHostCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes(true), MediaTypeNames.Application.Json));

            await _communication.StopEngine(_engineHostId, _deploymentId, CancellationToken.None);

            await _mqttClient.Received(1).Request(
                Arg.Is($"{_engineHostId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("stop");
                    JsonSerializer.Deserialize<Guid>(r.Payload!.Value.FirstSpan).Should().Be(_deploymentId);
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineHostId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.StopEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*stop*not possible*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_empty()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.StopEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*''*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_not_parseable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToBytes("asdf"), MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.StopEngine(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'asdf'*");
        }

        [Fact]
        public async Task Returns_received_result()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes(false), MediaTypeNames.Application.Json));

            var actual = await _communication.StopEngine(_engineHostId, Guid.Empty, CancellationToken.None);

            actual.Should().BeFalse();
        }
    }

    public class GetEngineState : MqttEngineHostCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes("0"), MediaTypeNames.Application.Json));

            await _communication.GetEngineState(_engineHostId, _deploymentId, CancellationToken.None);

            await _mqttClient.Received(1).Request(
                Arg.Is($"{_engineHostId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("getstate");
                    JsonSerializer.Deserialize<Guid>(r.Payload!.Value.FirstSpan).Should().Be(_deploymentId);
                    r.ContentType.Should().Be(MediaTypeNames.Application.Json);
                })!,
                Arg.Is($"{_engineHostId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*getstate*not possible*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_empty()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", ReadOnlySequence<byte>.Empty, MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*''*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_not_parseable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes("asdf"), MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'\"asdf\"'*");
        }

        [Fact]
        public async Task Throws_NotSupportedException_if_received_state_is_not_translatable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes("89"), MediaTypeNames.Application.Json));

            var act = FluentActions.Awaiting(() => _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None));

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*state*'89'*not*supported*");
        }

        [Fact]
        public async Task Returns_received_string_state()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes("Running"), MediaTypeNames.Application.Json));

            var actual = await _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None);

            actual.Should().Be(EngineState.Running);
        }

        [Fact]
        public async Task Returns_received_int_state()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToBytes(3), MediaTypeNames.Application.Json));

            var actual = await _communication.GetEngineState(_engineHostId, Guid.Empty, CancellationToken.None);

            actual.Should().Be(EngineState.Running);
        }
    }

    public class GetDeploymentStates : MqttEngineHostCommunicationFacts
    {
        [Fact]
        public async Task Sends_request_with_correct_topics_and_data()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", SerializeJsonToGzipBytes(new Dictionary<string, EngineState>() { { _deploymentId.ToString(), EngineState.Starting } }), MediaTypeNames.Application.GZip));

            await _communication.GetDeploymentStates(_engineHostId, CancellationToken.None);

            await _mqttClient.Received(1).Request(
                Arg.Is($"{_engineHostId}/request"),
                FluentArg.Assert<MqttRpcRequest>(r =>
                {
                    r.Method.Should().Be("getdeploymentstates");
                    ReadStringFromGzipBytes(r.Payload).Should().Be("{}");
                    r.ContentType.Should().Be(MediaTypeNames.Application.GZip);
                })!,
                Arg.Is($"{_engineHostId}/response"), Arg.Is(CancellationToken.None));
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_response_indicates_failure()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("fail", PlainTextToBytes("not possible"), MediaTypeNames.Text.Plain));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not*execute*getdeploymentstates*not possible*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_deployment_identifier_is_empty()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"":"Starting"}"""), MediaTypeNames.Application.GZip));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'<gzip compressed content>'*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_deployment_identifier_is_not_parseable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes("""{"asdf":"Starting"}"""), MediaTypeNames.Application.GZip));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'<gzip compressed content>'*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_empty()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"{{_deploymentId}}":""}"""), MediaTypeNames.Application.GZip));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'<gzip compressed content>'*");
        }

        [Fact]
        public async Task Throws_InvalidOperationException_if_received_state_is_not_parseable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"{{_deploymentId}}":"asdf"}"""), MediaTypeNames.Application.GZip));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot*parse*response*'<gzip compressed content>'*");
        }

        [Fact]
        public async Task Throws_NotSupportedException_if_received_state_is_not_translatable()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"{{_deploymentId}}":89}"""), MediaTypeNames.Application.GZip));

            var act = FluentActions.Awaiting(() => _communication.GetDeploymentStates(_engineHostId, CancellationToken.None));

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*state*'89'*not*supported*");
        }

        [Fact]
        public async Task Returns_received_string_state()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"{{_deploymentId}}":"Running"}"""), MediaTypeNames.Application.GZip));

            var actual = await _communication.GetDeploymentStates(_engineHostId, CancellationToken.None);

            actual.Should().ContainKey(_deploymentId).WhoseValue.Should().Be(EngineState.Running);
        }

        [Fact]
        public async Task Returns_received_int_state()
        {
            _mqttClient.Request(default!, default!, default!, default)
                .ReturnsForAnyArgs(new MqttRpcResponse("ok", PlainTextToGzipBytes($$"""{"{{_deploymentId}}":3}"""), MediaTypeNames.Application.GZip));

            var actual = await _communication.GetDeploymentStates(_engineHostId, CancellationToken.None);

            actual.Should().ContainKey(_deploymentId).WhoseValue.Should().Be(EngineState.Running);
        }
    }
}
