using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace ClusterManagement.Client.Tests.IoddManagement.Services;

public class IoddServiceTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private void AnswerAddIodd(IoddService service, Func<AddIodd, IoddChanged> eventFactory)
        => _mediator.Send(Arg.Any<AddIodd>(), Arg.Any<CancellationToken>())
            .Returns(callinfo =>
            {
                var command = callinfo.Arg<AddIodd>();
                return service.Consume(new ClientContext<IoddChanged>(eventFactory(command), command.CorrelationId), CancellationToken.None);
            });

    [Fact]
    public async Task AddIodd_should_return_success_when_iodd_was_added()
    {
        // Arrange
        using var service = new IoddService(_mediator);
        var device = DeviceInfoFactory.CreateDeviceInfo();
        AnswerAddIodd(service, cmd => new IoddChanged(new IoddId(888, 7), device, CrudAction.Created) { CorrelationId = cmd.CorrelationId });

        // Act
        var result = await service.AddIodd("iodd.zip", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<IoddServiceSuccessResult>();
    }

    [Fact]
    public async Task AddIodd_should_return_existing_device_when_iodd_already_exists()
    {
        // Arrange
        using var service = new IoddService(_mediator);
        var existingDevice = DeviceInfoFactory.CreateDeviceInfo();
        AnswerAddIodd(service, cmd => new IoddChanged(new IoddId(888, 7), existingDevice, CrudAction.Created, new ErrorInfo(IoddChanged.IoddAlreadyExists, "already exists"))
        {
            CorrelationId = cmd.CorrelationId
        });

        // Act
        var result = await service.AddIodd("iodd.zip", TestContext.Current.CancellationToken);

        // Assert
        var error = result.Should().BeOfType<IoddServiceErrorResult>().Subject;
        error.ErrorCode.Should().Be(IoddChanged.IoddAlreadyExists);
        error.ErrorMessage.Should().Be("already exists");
        error.ExistingDevice.Should().BeSameAs(existingDevice);
        service.ExistingDeviceCount.Should().Be(0);
    }

    [Fact]
    public async Task Consume_should_not_keep_existing_device_of_an_upload_it_does_not_wait_for()
    {
        // Arrange
        using var service = new IoddService(_mediator);
        var correlationId = Guid.NewGuid();
        var change = new IoddChanged(new IoddId(888, 7), DeviceInfoFactory.CreateDeviceInfo(), CrudAction.Created, new ErrorInfo(IoddChanged.IoddAlreadyExists, "already exists"))
        {
            CorrelationId = correlationId
        };

        // Act
        await service.Consume(new ClientContext<IoddChanged>(change, correlationId), TestContext.Current.CancellationToken);

        // Assert
        service.ExistingDeviceCount.Should().Be(0);
    }

    [Fact]
    public async Task AddIodd_should_return_error_without_device_for_other_errors()
    {
        // Arrange
        using var service = new IoddService(_mediator);
        AnswerAddIodd(service, cmd => new IoddChanged(new IoddId(0, 0), null, CrudAction.Created, new ErrorInfo(IoddChanged.ExtractZipError, "broken"))
        {
            CorrelationId = cmd.CorrelationId
        });

        // Act
        var result = await service.AddIodd("iodd.zip", TestContext.Current.CancellationToken);

        // Assert
        var error = result.Should().BeOfType<IoddServiceErrorResult>().Subject;
        error.ErrorCode.Should().Be(IoddChanged.ExtractZipError);
        error.ExistingDevice.Should().BeNull();
    }
}
