using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using EnterpriseTicketing.Application.Tickets.Queries.GetTicketsByCustomer;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Queries;

/// <summary>
/// Unit tests for <see cref="GetTicketsByCustomerQueryHandler"/>.
/// Verifies customer-scoped ticket retrieval and DTO mapping.
/// </summary>
public sealed class GetTicketsByCustomerQueryHandlerTests
{
    private readonly Mock<ITicketRepository>            _ticketRepo = new();
    private readonly GetTicketsByCustomerQueryHandler   _handler;

    public GetTicketsByCustomerQueryHandlerTests()
    {
        _handler = new GetTicketsByCustomerQueryHandler(_ticketRepo.Object);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_returns_all_tickets_for_customer()
    {
        var customerId = Guid.NewGuid();
        var tickets = BuildTicketsForCustomer(customerId, count: 4);
        SetupRepo(customerId, tickets);

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        result.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_maps_ticket_fields_to_dto_correctly()
    {
        var customerId = Guid.NewGuid();
        var ticket = Ticket.Create(
            TicketNumber.Create(2026, 11),
            "Invoice discrepancy",
            "Customer is disputing charge on last month's invoice.",
            TicketPriority.Medium,
            TicketCategory.Billing,
            customerId);

        SetupRepo(customerId, [ticket]);

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        var dto = result.Single();
        dto.Title.Should().Be("Invoice discrepancy");
        dto.Status.Should().Be(nameof(TicketStatus.Open));
        dto.Priority.Should().Be(nameof(TicketPriority.Medium));
        dto.Category.Should().Be(nameof(TicketCategory.Billing));
        dto.CustomerId.Should().Be(customerId);
        dto.EscalationCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_dto_ticket_number_uses_string_value()
    {
        var customerId = Guid.NewGuid();
        var ticket = Ticket.Create(
            TicketNumber.Create(2026, 77),
            "Ticket number test",
            "Verifying TicketNumber value object maps correctly to DTO string.",
            TicketPriority.Low,
            TicketCategory.General,
            customerId);

        SetupRepo(customerId, [ticket]);

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        result.Single().TicketNumber.Should().Be(ticket.TicketNumber.Value);
    }

    [Fact]
    public async Task Handle_returns_empty_list_when_customer_has_no_tickets()
    {
        var customerId = Guid.NewGuid();
        SetupRepo(customerId, []);

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_passes_correct_customer_id_to_repository()
    {
        var customerId = Guid.NewGuid();
        SetupRepo(customerId, []);

        await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        _ticketRepo.Verify(
            r => r.GetByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_leak_tickets_from_other_customers()
    {
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        var customer1Tickets = BuildTicketsForCustomer(customer1, count: 2);
        SetupRepo(customer1, customer1Tickets);
        SetupRepo(customer2, []); // customer2 has no tickets

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customer2 },
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_returns_readonly_list()
    {
        var customerId = Guid.NewGuid();
        SetupRepo(customerId, BuildTicketsForCustomer(customerId, count: 2));

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        result.Should().BeAssignableTo<IReadOnlyList<TicketSummaryDto>>();
    }

    [Fact]
    public async Task Handle_maps_multiple_tickets_preserving_order()
    {
        var customerId = Guid.NewGuid();
        var tickets = BuildTicketsForCustomer(customerId, count: 3);
        SetupRepo(customerId, tickets);

        var result = await _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        // All 3 customer IDs should match
        result.Should().AllSatisfy(dto =>
            dto.CustomerId.Should().Be(customerId));
    }

    [Fact]
    public async Task Handle_repository_failure_propagates_exception()
    {
        var customerId = Guid.NewGuid();
        _ticketRepo
            .Setup(r => r.GetByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dataverse connection lost"));

        var act = () => _handler.Handle(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("Dataverse connection lost");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupRepo(Guid customerId, IReadOnlyList<Ticket> tickets) =>
        _ticketRepo
            .Setup(r => r.GetByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tickets);

    private static IReadOnlyList<Ticket> BuildTicketsForCustomer(Guid customerId, int count) =>
        Enumerable.Range(1, count)
            .Select(i => Ticket.Create(
                TicketNumber.Create(2026, 100 + i),
                $"Customer ticket {i}",
                $"Issue number {i} reported by this customer, requiring investigation.",
                TicketPriority.Medium,
                TicketCategory.TechnicalSupport,
                customerId))
            .ToList();
}
