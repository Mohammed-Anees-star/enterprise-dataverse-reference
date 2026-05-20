using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Application.Tickets.Commands.EscalateTicket;
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
/// Unit tests for <see cref="EscalateTicketCommandHandler"/>.
/// Verifies escalation count, priority auto-upgrade, outbox dispatch, and guard logic.
/// </summary>
public sealed class EscalateTicketCommandHandlerTests
{
    private readonly Mock<ITicketRepository>      _ticketRepo = new();
    private readonly Mock<IOutboxStore>           _outbox     = new();
    private readonly Mock<ICurrentUserService>    _userSvc    = new();
    private readonly EscalateTicketCommandHandler _handler;

    public EscalateTicketCommandHandlerTests()
    {
        _userSvc.Setup(u => u.UserId).Returns("test-user");

        _handler = new EscalateTicketCommandHandler(
            _ticketRepo.Object,
            _outbox.Object,
            _userSvc.Object,
            NullLogger<EscalateTicketCommandHandler>.Instance);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_increments_escalation_count()
    {
        var (ticket, command) = SetupTicket();

        await _handler.Handle(command, CancellationToken.None);

        ticket.EscalationCount.Should().Be(1);
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
    public async Task Handle_appends_escalated_event_to_outbox()
    {
        var (_, command) = SetupTicket();

        await _handler.Handle(command, CancellationToken.None);

        _outbox.Verify(
            o => o.AppendAsync(It.IsAny<TicketEscalatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_clears_domain_events_after_outbox_append()
    {
        var (ticket, command) = SetupTicket();

        await _handler.Handle(command, CancellationToken.None);

        ticket.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_multiple_escalations_increment_count_correctly()
    {
        var ticket = BuildOpenTicket(TicketPriority.Low);
        SetupRepoForTicket(ticket);

        var command = new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Still unresolved" };

        await _handler.Handle(command, CancellationToken.None);
        ticket.ClearDomainEvents(); // simulate second round

        await _handler.Handle(command, CancellationToken.None);
        ticket.ClearDomainEvents();

        await _handler.Handle(command, CancellationToken.None);

        ticket.EscalationCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_auto_upgrades_priority_to_critical_on_third_escalation()
    {
        var ticket = BuildOpenTicket(TicketPriority.Low);
        SetupRepoForTicket(ticket);

        var command = new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Urgent" };

        // Escalate 3 times — domain logic auto-upgrades to Critical at count >= 3
        await _handler.Handle(command, CancellationToken.None);
        ticket.ClearDomainEvents();
        await _handler.Handle(command, CancellationToken.None);
        ticket.ClearDomainEvents();
        await _handler.Handle(command, CancellationToken.None);

        ticket.Priority.Should().Be(TicketPriority.Critical);
    }

    [Fact]
    public async Task Handle_critical_priority_ticket_stays_critical_after_escalation()
    {
        var ticket = BuildOpenTicket(TicketPriority.Critical);
        SetupRepoForTicket(ticket);

        var command = new EscalateTicketCommand { TicketId = ticket.Id, Reason = "VIP customer" };

        await _handler.Handle(command, CancellationToken.None);

        ticket.Priority.Should().Be(TicketPriority.Critical);
        ticket.EscalationCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_falls_back_to_system_when_user_id_is_null()
    {
        _userSvc.Setup(u => u.UserId).Returns((string?)null);
        var (_, command) = SetupTicket();

        var act = () => _handler.Handle(command, CancellationToken.None);

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
            new EscalateTicketCommand { TicketId = Guid.NewGuid(), Reason = "Test" },
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_closed_ticket_throws_domain_exception()
    {
        var ticket = BuildOpenTicket(TicketPriority.Medium);
        ticket.Close("user-1");
        ticket.ClearDomainEvents();
        SetupRepoForTicket(ticket);

        var act = () => _handler.Handle(
            new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Late escalation" },
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>(); // InvalidTicketStateException from domain
    }

    [Fact]
    public async Task Handle_resolved_ticket_throws_domain_exception()
    {
        var ticket = BuildOpenTicket(TicketPriority.Medium);
        ticket.Resolve("Issue fixed", "user-1");
        ticket.ClearDomainEvents();
        SetupRepoForTicket(ticket);

        var act = () => _handler.Handle(
            new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Customer unhappy" },
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>(); // InvalidTicketStateException from domain
    }

    [Fact]
    public async Task Handle_repository_failure_propagates_exception()
    {
        var ticket = BuildOpenTicket(TicketPriority.Medium);
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _ticketRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dataverse timeout"));

        var act = () => _handler.Handle(
            new EscalateTicketCommand { TicketId = ticket.Id, Reason = "Urgent" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("Dataverse timeout");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (Ticket ticket, EscalateTicketCommand command) SetupTicket(
        TicketPriority priority = TicketPriority.Medium)
    {
        var ticket = BuildOpenTicket(priority);
        SetupRepoForTicket(ticket);
        return (ticket, new EscalateTicketCommand { TicketId = ticket.Id, Reason = "SLA breach" });
    }

    private void SetupRepoForTicket(Ticket ticket) =>
        _ticketRepo
            .Setup(r => r.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

    private static Ticket BuildOpenTicket(TicketPriority priority) =>
        Ticket.Create(
            TicketNumber.Create(2026, 99),
            "Repeated login failures",
            "Multiple users report they cannot authenticate to the VPN.",
            priority,
            TicketCategory.TechnicalSupport,
            Guid.NewGuid());
}
