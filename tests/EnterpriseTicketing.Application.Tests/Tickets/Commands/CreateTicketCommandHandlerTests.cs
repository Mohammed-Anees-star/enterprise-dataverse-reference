using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Commands;

/// <summary>
/// Unit tests for <see cref="CreateTicketCommandHandler"/>.
/// All infrastructure dependencies are replaced with Moq doubles.
/// These tests run in < 5 ms — no network, no process boundary.
/// </summary>
public sealed class CreateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository>      _ticketRepo     = new();
    private readonly Mock<ICustomerRepository>    _customerRepo   = new();
    private readonly Mock<IOutboxStore>           _outbox         = new();
    private readonly Mock<ITicketNumberSequence>  _sequence       = new();
    private readonly CreateTicketCommandHandler   _handler;

    public CreateTicketCommandHandlerTests()
    {
        _sequence
            .Setup(s => s.NextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new CreateTicketCommandHandler(
            _ticketRepo.Object,
            _customerRepo.Object,
            _outbox.Object,
            _sequence.Object,
            NullLogger<CreateTicketCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_valid_command_returns_non_empty_guid()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerExists(customerId, true);
        SetupTicketAdd();

        var id = await _handler.Handle(BuildCommand(customerId), CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_valid_command_persists_ticket_with_correct_fields()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerExists(customerId, true);

        Ticket? captured = null;
        _ticketRepo
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((t, _) => captured = t);

        await _handler.Handle(new CreateTicketCommand
        {
            Title       = "Disk almost full",
            Description = "Root partition at 95%",
            Priority    = TicketPriority.Critical,
            Category    = TicketCategory.TechnicalSupport,
            CustomerId  = customerId
        }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Title.Should().Be("Disk almost full");
        captured.Priority.Should().Be(TicketPriority.Critical);
        captured.CustomerId.Should().Be(customerId);
        captured.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_valid_command_appends_domain_event_to_outbox()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerExists(customerId, true);
        SetupTicketAdd();

        await _handler.Handle(BuildCommand(customerId), CancellationToken.None);

        // TicketCreated event must be written to the outbox, NOT directly to Service Bus
        _outbox.Verify(
            o => o.AppendAsync(It.IsAny<TicketCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_valid_command_uses_sequence_for_ticket_number()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerExists(customerId, true);
        SetupTicketAdd();

        await _handler.Handle(BuildCommand(customerId), CancellationToken.None);

        _sequence.Verify(
            s => s.NextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_nonexistent_customer_throws_not_found_exception()
    {
        SetupCustomerExists(Guid.NewGuid(), false);

        var act = () => _handler.Handle(BuildCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_when_repository_throws_exception_propagates()
    {
        var customerId = Guid.NewGuid();
        SetupCustomerExists(customerId, true);

        _ticketRepo
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dataverse unavailable"));

        var act = () => _handler.Handle(BuildCommand(customerId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("Dataverse unavailable");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupCustomerExists(Guid id, bool exists) =>
        _customerRepo
            .Setup(r => r.ExistsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);

    private void SetupTicketAdd() =>
        _ticketRepo
            .Setup(r => r.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private static CreateTicketCommand BuildCommand(Guid customerId) => new()
    {
        Title       = "Test Ticket",
        Description = "Test Description",
        Priority    = TicketPriority.Medium,
        Category    = TicketCategory.Billing,
        CustomerId  = customerId
    };
}
