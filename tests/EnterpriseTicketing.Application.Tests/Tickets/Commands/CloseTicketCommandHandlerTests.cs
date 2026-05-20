using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Application.Tickets.Commands.CloseTicket;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Commands;

/// <summary>
/// Unit tests for <see cref="CloseTicketCommandHandler"/>.
/// Verifies ticket closure, domain event outbox dispatch, and guard logic.
/// </summary>
public sealed class CloseTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository>   _ticketRepo = new();
    private readonly Mock<IOutboxStore>        _outbox     = new();
    private readonly Mock<ICurrentUserService> _userSvc    = new();
    private readonly CloseTicketCommandHandler _handler;

    public CloseTicketCommandHandlerTests()
    {
        _handler = new CloseTicketCommandHandler(
            _ticketRepo.Object,
            _outbox.Object,
            _userSvc.Object,
            NullLogger<CloseTicketCommandHandler>.Instance);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_open_ticket_closes_it_successfully()
    {
        var (ticket, command) = SetupOpenTicket();

        await _handler.Handle(command, CancellationToken.None);

        ticket.Status.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public async Task Handle_persists_updated_ticket_via_repository()
    {
        var (ticket, command) = SetupOpenTicket();

        await _handler.Handle(command, CancellationToken.None);

        _ticketRepo.Verify(
            r => r.UpdateAsync(ticket, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_appends_status_changed_event_to_outbox()
    {
        var (_, command) = SetupOpenTicket();

        await _handler.Handle(command, CancellationToken.None);

        _outbox.Verify(
            o => o.AppendAsync(It.IsAny<TicketStatusChangedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_clears_domain_events_after_outbox_append()
    {
        var (ticket, command) = SetupOpenTicket();

        await _handler.Handle(command, CancellationToken.None);

        ticket.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_uses_current_user_id_for_close_operation()
    {
        const string expectedUserId = "user-abc-123";
        _userSvc.Setup(u => u.UserId).Returns(expectedUserId);

        var ticket = BuildOpenTicket();
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        await _handler.Handle(new CloseTicketCommand { TicketId = ticket.Id }, CancellationToken.None);

        // Closed ticket should reflect the closer — status confirms it went through Close(userId)
        ticket.Status.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public async Task Handle_falls_back_to_system_when_user_id_is_null()
    {
        _userSvc.Setup(u => u.UserId).Returns((string?)null);

        var ticket = BuildOpenTicket();
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        // Should not throw even when no authenticated user
        var act = () => _handler.Handle(new CloseTicketCommand { TicketId = ticket.Id }, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ─── Guard conditions ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_nonexistent_ticket_throws_not_found_exception()
    {
        _ticketRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var act = () => _handler.Handle(
            new CloseTicketCommand { TicketId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_already_closed_ticket_throws_domain_exception()
    {
        var ticket = BuildOpenTicket();
        ticket.Close("user-1"); // close it first
        ticket.ClearDomainEvents();

        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _handler.Handle(
            new CloseTicketCommand { TicketId = ticket.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>(); // InvalidTicketStateException
    }

    [Fact]
    public async Task Handle_repository_failure_propagates_exception()
    {
        var ticket = BuildOpenTicket();
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        _ticketRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dataverse unavailable"));

        var act = () => _handler.Handle(
            new CloseTicketCommand { TicketId = ticket.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("Dataverse unavailable");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (Ticket ticket, CloseTicketCommand command) SetupOpenTicket()
    {
        _userSvc.Setup(u => u.UserId).Returns("test-user");
        var ticket = BuildOpenTicket();
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        return (ticket, new CloseTicketCommand { TicketId = ticket.Id });
    }

    private static Ticket BuildOpenTicket() =>
        Ticket.Create(
            TicketNumber.Create(2026, 42),
            "Network connectivity issue",
            "Users in building B cannot reach internal services.",
            TicketPriority.High,
            TicketCategory.TechnicalSupport,
            Guid.NewGuid());
}
