using EnterpriseTicketing.Application.Common.Models;
using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Queries;

/// <summary>
/// Unit tests for <see cref="GetTicketsQueryHandler"/>.
/// Verifies pagination, filtering delegation, DTO mapping, and edge cases.
/// </summary>
public sealed class GetTicketsQueryHandlerTests
{
    private readonly Mock<ITicketRepository>  _ticketRepo = new();
    private readonly GetTicketsQueryHandler   _handler;

    public GetTicketsQueryHandlerTests()
    {
        _handler = new GetTicketsQueryHandler(_ticketRepo.Object);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_returns_paginated_list_of_mapped_dtos()
    {
        var tickets = BuildTickets(3);
        SetupRepo(tickets, totalCount: 3);

        var result = await _handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_maps_ticket_fields_to_dto_correctly()
    {
        var customerId = Guid.NewGuid();
        var ticket = Ticket.Create(
            TicketNumber.Create(2026, 1),
            "DNS resolution failure",
            "Internal DNS is not resolving certain hostnames.",
            TicketPriority.High,
            TicketCategory.TechnicalSupport,
            customerId);

        SetupRepo([ticket], totalCount: 1);

        var result = await _handler.Handle(new GetTicketsQuery(), CancellationToken.None);
        var dto = result.Items.Single();

        dto.Title.Should().Be("DNS resolution failure");
        dto.Status.Should().Be(nameof(TicketStatus.Open));
        dto.Priority.Should().Be(nameof(TicketPriority.High));
        dto.Category.Should().Be(nameof(TicketCategory.TechnicalSupport));
        dto.CustomerId.Should().Be(customerId);
        dto.EscalationCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_returns_correct_pagination_metadata()
    {
        var tickets = BuildTickets(5);
        SetupRepo(tickets, totalCount: 47);

        var result = await _handler.Handle(
            new GetTicketsQuery { PageNumber = 2, PageSize = 5 },
            CancellationToken.None);

        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalCount.Should().Be(47);
        result.TotalPages.Should().Be(10); // ceil(47/5)
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_last_page_has_no_next_page()
    {
        var tickets = BuildTickets(2);
        SetupRepo(tickets, totalCount: 22);

        var result = await _handler.Handle(
            new GetTicketsQuery { PageNumber = 11, PageSize = 2 },
            CancellationToken.None);

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_first_page_has_no_previous_page()
    {
        var tickets = BuildTickets(3);
        SetupRepo(tickets, totalCount: 30);

        var result = await _handler.Handle(
            new GetTicketsQuery { PageNumber = 1, PageSize = 3 },
            CancellationToken.None);

        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_empty_result_returns_empty_paginated_list()
    {
        SetupRepo([], totalCount: 0);

        var result = await _handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Handle_passes_filter_parameters_to_repository()
    {
        SetupRepo([], totalCount: 0);

        var query = new GetTicketsQuery
        {
            Status             = TicketStatus.InProgress,
            Priority           = TicketPriority.Critical,
            Category           = TicketCategory.Billing,
            CustomerId         = Guid.NewGuid(),
            AssignedToUserId   = "agent-007",
            SearchTerm         = "payment",
            SortBy             = "UpdatedAt",
            SortDescending     = false,
            PageNumber         = 2,
            PageSize           = 10
        };

        await _handler.Handle(query, CancellationToken.None);

        _ticketRepo.Verify(r => r.GetPagedAsync(
            It.Is<TicketFilter>(f =>
                f.Status           == TicketStatus.InProgress &&
                f.Priority         == TicketPriority.Critical &&
                f.Category         == TicketCategory.Billing &&
                f.AssignedToUserId == "agent-007" &&
                f.SearchTerm       == "payment" &&
                f.SortBy           == "UpdatedAt" &&
                f.SortDescending   == false),
            2,
            10,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_default_query_uses_page1_size20_sort_by_created_desc()
    {
        SetupRepo([], totalCount: 0);

        await _handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        _ticketRepo.Verify(r => r.GetPagedAsync(
            It.Is<TicketFilter>(f =>
                f.SortBy       == "CreatedAt" &&
                f.SortDescending == true),
            1,
            20,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_dto_ticket_number_uses_string_value()
    {
        var ticket = Ticket.Create(
            TicketNumber.Create(2026, 500),
            "Test ticket",
            "Testing ticket number mapping in DTOs.",
            TicketPriority.Low,
            TicketCategory.General,
            Guid.NewGuid());

        SetupRepo([ticket], totalCount: 1);

        var result = await _handler.Handle(new GetTicketsQuery(), CancellationToken.None);

        result.Items.Single().TicketNumber.Should().Be(ticket.TicketNumber.Value);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupRepo(IReadOnlyList<Ticket> tickets, int totalCount) =>
        _ticketRepo
            .Setup(r => r.GetPagedAsync(
                It.IsAny<TicketFilter>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((tickets, totalCount));

    private static IReadOnlyList<Ticket> BuildTickets(int count) =>
        Enumerable.Range(1, count)
            .Select(i => Ticket.Create(
                TicketNumber.Create(2026, i),
                $"Ticket {i}",
                $"Description for ticket number {i} with enough detail.",
                TicketPriority.Medium,
                TicketCategory.TechnicalSupport,
                Guid.NewGuid()))
            .ToList();
}
