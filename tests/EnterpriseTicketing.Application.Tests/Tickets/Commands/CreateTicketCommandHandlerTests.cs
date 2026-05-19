using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Commands;

/// <summary>
/// Application handler tests use Moq to substitute infrastructure dependencies.
/// Tests verify orchestration logic: correct repository calls, event publishing,
/// error propagation, and return values.
///
/// These tests run without real Dataverse or Service Bus connections.
/// </summary>
public sealed class CreateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository> _ticketRepositoryMock;
    private readonly Mock<ICustomerRepository> _customerRepositoryMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ILogger<CreateTicketCommandHandler>> _loggerMock;
    private readonly CreateTicketCommandHandler _handler;

    public CreateTicketCommandHandlerTests()
    {
        _ticketRepositoryMock = new Mock<ITicketRepository>();
        _customerRepositoryMock = new Mock<ICustomerRepository>();
        _eventBusMock = new Mock<IEventBus>();
        _loggerMock = new Mock<ILogger<CreateTicketCommandHandler>>();

        _handler = new CreateTicketCommandHandler(
            _ticketRepositoryMock.Object,
            _customerRepositoryMock.Object,
            _eventBusMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsTicketId()
    {
        var customerId = Guid.NewGuid();
        _customerRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ticketRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateTicketCommand
        {
            Title = "Production system down",
            Description = "Users cannot log in since 3pm",
            Priority = TicketPriority.Critical,
            Category = TicketCategory.TechnicalSupport,
            CustomerId = customerId
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsNotFoundException()
    {
        var customerId = Guid.NewGuid();
        _customerRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateTicketCommand
        {
            Title = "Issue",
            Description = "Description",
            Priority = TicketPriority.Low,
            Category = TicketCategory.GeneralInquiry,
            CustomerId = customerId
        };

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesDomainEvent()
    {
        var customerId = Guid.NewGuid();
        _customerRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ticketRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new CreateTicketCommand
        {
            Title = "Test Ticket",
            Description = "Test Description",
            Priority = TicketPriority.High,
            Category = TicketCategory.Bug,
            CustomerId = customerId
        };

        await _handler.Handle(command, CancellationToken.None);

        // Verify that at least one domain event was published (TicketCreatedEvent)
        _eventBusMock.Verify(
            e => e.PublishAsync(It.IsAny<Domain.Events.IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsTicket()
    {
        var customerId = Guid.NewGuid();
        _customerRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Ticket? capturedTicket = null;
        _ticketRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((t, _) => capturedTicket = t)
            .Returns(Task.CompletedTask);

        var command = new CreateTicketCommand
        {
            Title = "Captured Ticket",
            Description = "Description",
            Priority = TicketPriority.Medium,
            Category = TicketCategory.Billing,
            CustomerId = customerId
        };

        await _handler.Handle(command, CancellationToken.None);

        capturedTicket.Should().NotBeNull();
        capturedTicket!.Title.Should().Be("Captured Ticket");
        capturedTicket.CustomerId.Should().Be(customerId);
        capturedTicket.Status.Should().Be(EnterpriseTicketing.Domain.Enums.TicketStatus.Open);
    }
}
