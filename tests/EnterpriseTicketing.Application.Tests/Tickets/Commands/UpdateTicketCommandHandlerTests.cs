using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Tickets.Commands.UpdateTicket;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateTicketCommandHandler"/>.
/// Verifies field updates, repository persistence, and guard logic.
/// </summary>
public sealed class UpdateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository>    _ticketRepo = new();
    private readonly UpdateTicketCommandHandler _handler;

    public UpdateTicketCommandHandlerTests()
    {
        _handler = new UpdateTicketCommandHandler(
            _ticketRepo.Object,
            NullLogger<UpdateTicketCommandHandler>.Instance);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_updates_title_on_open_ticket()
    {
        var (ticket, _) = SetupTicket();
        var command = BuildCommand(ticket.Id, title: "Updated Title");

        await _handler.Handle(command, CancellationToken.None);

        ticket.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Handle_updates_description_on_open_ticket()
    {
        var (ticket, _) = SetupTicket();
        var command = BuildCommand(ticket.Id, description: "New detailed description for this ticket.");

        await _handler.Handle(command, CancellationToken.None);

        ticket.Description.Should().Be("New detailed description for this ticket.");
    }

    [Fact]
    public async Task Handle_updates_priority_on_open_ticket()
    {
        var (ticket, _) = SetupTicket(priority: TicketPriority.Low);
        var command = BuildCommand(ticket.Id, priority: TicketPriority.Critical);

        await _handler.Handle(command, CancellationToken.None);

        ticket.Priority.Should().Be(TicketPriority.Critical);
    }

    [Fact]
    public async Task Handle_updates_category_on_open_ticket()
    {
        var (ticket, _) = SetupTicket(category: TicketCategory.Billing);
        var command = BuildCommand(ticket.Id, category: TicketCategory.TechnicalSupport);

        await _handler.Handle(command, CancellationToken.None);

        ticket.Category.Should().Be(TicketCategory.TechnicalSupport);
    }

    [Fact]
    public async Task Handle_persists_ticket_via_repository()
    {
        var (ticket, command) = SetupTicket();

        await _handler.Handle(command, CancellationToken.None);

        _ticketRepo.Verify(
            r => r.UpdateAsync(ticket, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_updates_all_fields_in_single_call()
    {
        var (ticket, _) = SetupTicket();

        var command = new UpdateTicketCommand
        {
            TicketId    = ticket.Id,
            Title       = "Comprehensive Update",
            Description = "Full description rewrite with more context.",
            Priority    = TicketPriority.High,
            Category    = TicketCategory.General
        };

        await _handler.Handle(command, CancellationToken.None);

        ticket.Title.Should().Be("Comprehensive Update");
        ticket.Description.Should().Be("Full description rewrite with more context.");
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.Category.Should().Be(TicketCategory.General);
    }

    [Fact]
    public async Task Handle_does_not_raise_domain_events_on_update()
    {
        var (ticket, command) = SetupTicket();
        ticket.ClearDomainEvents(); // clear creation event

        await _handler.Handle(command, CancellationToken.None);

        // UpdateDetails does not raise domain events — purely a field update
        ticket.DomainEvents.Should().BeEmpty();
    }

    // ─── Guard conditions ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_nonexistent_ticket_throws_not_found_exception()
    {
        _ticketRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var act = () => _handler.Handle(
            BuildCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_closed_ticket_throws_domain_exception()
    {
        var ticket = BuildTicket(TicketPriority.Medium, TicketCategory.Billing);
        ticket.Close("user-1");
        ticket.ClearDomainEvents();

        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _handler.Handle(BuildCommand(ticket.Id), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>(); // InvalidTicketStateException from domain
    }

    [Fact]
    public async Task Handle_cancelled_ticket_throws_domain_exception()
    {
        var ticket = BuildTicket(TicketPriority.Medium, TicketCategory.Billing);
        ticket.ChangeStatus(TicketStatus.Cancelled, "user-1");
        ticket.ClearDomainEvents();

        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _handler.Handle(BuildCommand(ticket.Id), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>(); // InvalidTicketStateException from domain
    }

    [Fact]
    public async Task Handle_repository_failure_propagates_exception()
    {
        var (ticket, command) = SetupTicket();

        _ticketRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Write conflict"));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("Write conflict");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (Ticket ticket, UpdateTicketCommand command) SetupTicket(
        TicketPriority priority = TicketPriority.Medium,
        TicketCategory category = TicketCategory.TechnicalSupport)
    {
        var ticket = BuildTicket(priority, category);
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        return (ticket, BuildCommand(ticket.Id));
    }

    private static UpdateTicketCommand BuildCommand(
        Guid ticketId,
        string title        = "Updated Ticket Title",
        string description  = "Updated description with sufficient detail.",
        TicketPriority priority  = TicketPriority.Medium,
        TicketCategory category  = TicketCategory.TechnicalSupport) =>
        new()
        {
            TicketId    = ticketId,
            Title       = title,
            Description = description,
            Priority    = priority,
            Category    = category
        };

    private static Ticket BuildTicket(TicketPriority priority, TicketCategory category) =>
        Ticket.Create(
            TicketNumber.Create(2026, 7),
            "Original ticket title",
            "Original description that is long enough to be valid.",
            priority,
            category,
            Guid.NewGuid());
}
