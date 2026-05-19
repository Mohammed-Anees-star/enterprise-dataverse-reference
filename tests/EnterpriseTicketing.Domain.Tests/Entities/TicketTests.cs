using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Events;
using EnterpriseTicketing.Domain.Exceptions;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseTicketing.Domain.Tests.Entities;

/// <summary>
/// Domain tests validate pure business logic without any infrastructure dependencies.
/// These tests run in milliseconds — no database, no HTTP, no external dependencies.
/// This is the payoff of Clean Architecture: domain tests are fast, reliable, and exhaustive.
/// </summary>
public sealed class TicketTests
{
    private static readonly TicketNumber ValidTicketNumber = TicketNumber.Create(2025, 1);
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_ValidParameters_ReturnsTicketWithOpenStatus()
    {
        var ticket = Ticket.Create(
            ValidTicketNumber, "Test Title", "Test Description",
            TicketPriority.Medium, TicketCategory.TechnicalSupport, ValidCustomerId);

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Title.Should().Be("Test Title");
        ticket.Priority.Should().Be(TicketPriority.Medium);
        ticket.EscalationCount.Should().Be(0);
        ticket.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_RaisesTicketCreatedDomainEvent()
    {
        var ticket = Ticket.Create(
            ValidTicketNumber, "Title", "Description",
            TicketPriority.High, TicketCategory.Bug, ValidCustomerId);

        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketCreatedEvent>();

        var evt = (TicketCreatedEvent)ticket.DomainEvents[0];
        evt.TicketId.Should().Be(ticket.Id);
        evt.Priority.Should().Be(TicketPriority.High);
    }

    [Fact]
    public void Create_EmptyTitle_ThrowsArgumentException()
    {
        var act = () => Ticket.Create(
            ValidTicketNumber, string.Empty, "Description",
            TicketPriority.Low, TicketCategory.Billing, ValidCustomerId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TitleTooLong_ThrowsDomainException()
    {
        var longTitle = new string('x', 201);

        var act = () => Ticket.Create(
            ValidTicketNumber, longTitle, "Description",
            TicketPriority.Low, TicketCategory.Billing, ValidCustomerId);

        act.Should().Throw<DomainException>().WithMessage("*200*");
    }

    [Fact]
    public void Close_OpenTicket_SetsStatusToClosed()
    {
        var ticket = CreateOpenTicket();

        ticket.Close("user-123");

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void Close_AlreadyClosed_ThrowsInvalidTicketStateException()
    {
        var ticket = CreateOpenTicket();
        ticket.Close("user-1");

        var act = () => ticket.Close("user-2");

        act.Should().Throw<InvalidTicketStateException>();
    }

    [Fact]
    public void Escalate_OpenTicket_IncrementsEscalationCount()
    {
        var ticket = CreateOpenTicket();

        ticket.Escalate("SLA breach imminent", "manager-1");

        ticket.EscalationCount.Should().Be(1);
    }

    [Fact]
    public void Escalate_ThirdEscalation_AutoUpgradesToCriticalPriority()
    {
        var ticket = CreateOpenTicket();

        ticket.Escalate("Reason 1", "user-1");
        ticket.Escalate("Reason 2", "user-2");
        ticket.Escalate("Reason 3", "user-3");

        ticket.EscalationCount.Should().Be(3);
        ticket.Priority.Should().Be(TicketPriority.Critical);
    }

    [Fact]
    public void Escalate_ClosedTicket_ThrowsInvalidTicketStateException()
    {
        var ticket = CreateOpenTicket();
        ticket.Close("user-1");

        var act = () => ticket.Escalate("Late escalation", "user-2");

        act.Should().Throw<InvalidTicketStateException>();
    }

    [Fact]
    public void TicketNumber_ValidFormat_ParsesCorrectly()
    {
        var tn = TicketNumber.Parse("TKT-2025-000042");

        tn.Value.Should().Be("TKT-2025-000042");
        tn.ToString().Should().Be("TKT-2025-000042");
    }

    [Fact]
    public void TicketNumber_InvalidFormat_ThrowsArgumentException()
    {
        var act = () => TicketNumber.Parse("INVALID-FORMAT");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TicketNumber_Equality_SameValueAreEqual()
    {
        var t1 = TicketNumber.Parse("TKT-2025-000001");
        var t2 = TicketNumber.Parse("TKT-2025-000001");

        t1.Equals(t2).Should().BeTrue();
        t1.Should().Be(t2);
    }

    private static Ticket CreateOpenTicket() =>
        Ticket.Create(ValidTicketNumber, "Title", "Description",
            TicketPriority.Medium, TicketCategory.TechnicalSupport, ValidCustomerId);
}
