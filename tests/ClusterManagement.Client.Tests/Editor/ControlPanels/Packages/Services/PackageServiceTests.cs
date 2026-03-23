using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Iodds.Commands;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Services;

public class PackageServiceTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private static ClusterPackageInfo CreateModel(string? installedVersion = null)
        => new()
        { Name = "Test Repo", InstalledVersion = installedVersion };

    private static void SetupSendAndSimulateEvent<TCommand, TEvent>(
        IUiMediator mediator,
        PackageService service,
        Func<TCommand, TEvent> eventFactory)
        where TCommand : class, ICommand
        where TEvent : class, IEvent
        => mediator.When(m => m.Send(Arg.Any<TCommand>(), Arg.Any<CancellationToken>()))
            .Do(async callinfo =>
            {
                var command = callinfo.Arg<TCommand>();
                var @event = eventFactory(command);
                if (@event is ClusterPackageChanged packageChanged)
                {
                    var context = new ClientContext<ClusterPackageChanged>(packageChanged, command.CorrelationId);
                    await service.Consume(context, CancellationToken.None);
                }
                else if (@event is ClusterDependenciesChanged dependenciesChanged)
                {
                    var context = new ClientContext<ClusterDependenciesChanged>(dependenciesChanged, command.CorrelationId);
                    await service.Consume(context, CancellationToken.None);
                }
            });

    public sealed class UploadPackageTests : PackageServiceTests
    {
        [Fact]
        public async Task Should_send_create_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<AddClusterPackage, ClusterPackageChanged>(
                _mediator, service,
                cmd => new ClusterPackageChanged(model.Name, "1.0.2", ClusterPackageChange.Add)
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UploadPackage([], TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<PackageServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<AddClusterPackage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<AddClusterPackage, ClusterPackageChanged>(
                _mediator, service,
                cmd => new ClusterPackageChanged(model.Name, "1.0.2", ClusterPackageChange.Add, new ErrorInfo(100, "upload failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UploadPackage([], TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<PackageServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("upload failed");
        }
    }

    public sealed class UpdatePackagesTests : PackageServiceTests
    {
        private readonly ClusterDependency _dependency = new() { Name = "ViciOne.Test.Fb", Version = new Version("1.2.0") };

        [Fact]
        public async Task Should_send_create_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<UpdateClusterDependencies, ClusterDependenciesChanged>(
                _mediator, service,
                cmd => new ClusterDependenciesChanged([]) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdatePackages([], TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<PackageServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<UpdateClusterDependencies>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<UpdateClusterDependencies, ClusterDependenciesChanged>(
                _mediator, service,
                cmd => new ClusterDependenciesChanged([], new ErrorInfo(100, "create failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdatePackages([], TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<PackageServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("create failed");
        }

        [Fact]
        public async Task Should_fire_repository_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<UpdateClusterDependencies, ClusterDependenciesChanged>(
                _mediator, service,
                cmd => new ClusterDependenciesChanged([new ClusterDependencyChange(CrudAction.Created, operation)])
                { CorrelationId = cmd.CorrelationId });

            IReadOnlyCollection<ClusterDependencyChange>? changes = null;
            CancellationToken? cancellationToken = null;
            service.DependenciesChanged += (changed, token) => { changes = changed.Changes; cancellationToken = token; return Task.CompletedTask; };

            // Act
            await service.UpdatePackages([operation], TestContext.Current.CancellationToken);

            // Assert
            changes.Should().NotBeNullOrEmpty();
            changes.Should().ContainSingle(c => c.Action == CrudAction.Created && c.Operation.Dependency == _dependency);
        }
    }

    public sealed class DeleteRepositoryTests : PackageServiceTests
    {
        private readonly string _installedVersion = "1.0.4";

        [Fact]
        public async Task Should_send_delete_command_and_return_success()
        {
            // Arrange
            var model = CreateModel(_installedVersion);
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<DeleteClusterPackage, ClusterPackageChanged>(
                _mediator, service,
                cmd => new ClusterPackageChanged(model.Name, _installedVersion, ClusterPackageChange.Delete)
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.DeletePackage(model.Name, _installedVersion, CancellationToken.None);

            // Assert
            result.Should().BeOfType<PackageServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<DeleteClusterPackage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel(_installedVersion);
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<DeleteClusterPackage, ClusterPackageChanged>(
                _mediator, service,
                cmd => new ClusterPackageChanged(model.Name, _installedVersion, ClusterPackageChange.Delete, new ErrorInfo(100, "delete failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.DeletePackage(model.Name, _installedVersion, TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<PackageServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("delete failed");
        }

        [Fact]
        public async Task Should_fire_repository_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel(_installedVersion);
            using var service = new PackageService(_mediator);

            SetupSendAndSimulateEvent<DeleteClusterPackage, ClusterPackageChanged>(
                _mediator, service,
                cmd => new ClusterPackageChanged(model.Name, _installedVersion, ClusterPackageChange.Delete)
                { CorrelationId = cmd.CorrelationId });

            ClusterPackageChanged? changedPackage = null;
            CancellationToken? cancellationToken = null;
            service.PackageChanged += (change, token) => { changedPackage = change; cancellationToken = token; return Task.CompletedTask; };

            // Act
            await service.DeletePackage(model.Name, _installedVersion, TestContext.Current.CancellationToken);

            // Assert
            changedPackage.Should().NotBeNull();
            changedPackage.PackageName.Should().Be(model.Name);
            cancellationToken.Should().NotBeNull();
        }
    }
}
