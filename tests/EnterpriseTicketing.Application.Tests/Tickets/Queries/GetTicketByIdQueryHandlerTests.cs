using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Application.Tickets.Queries.GetTicketById;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseTicketing.Application.Tests.Tickets.Queries;

public class GetTicketByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_dto_when_ticket_exists()
    {
        var repo = new Mock<ITicketRepository>();
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.Create(
            TicketNumber.Create(2025, 1),
            "Title", "Description body that is long enough",
            TicketPriority.High, TicketCategory.Technical,
            Guid.NewGuid());

        repo.Setup(r => r.GetByIdAsync(ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var sut = new GetTicketByIdQueryHandler(repo.Object);
        var dto = await sut.Handle(new GetTicketByIdQuery { TicketId = ticketId }, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Title");
        dto.Priority.Should().Be(nameof(TicketPriority.High));
    }

    [Fact]
    public async Task Handle_throws_NotFound_when_repository_returns_null()
    {
        var repo = new Mock<ITicketRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var sut = new GetTicketByIdQueryHandler(repo.Object);

        Func<Task> act = () => sut.Handle(
            new GetTicketByIdQuery { TicketId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
