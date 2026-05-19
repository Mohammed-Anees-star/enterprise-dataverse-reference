using Asp.Versioning;
using EnterpriseTicketing.API.Models.Requests;
using EnterpriseTicketing.API.Models.Responses;
using EnterpriseTicketing.Application.Common.Models;
using EnterpriseTicketing.Application.Tickets.Commands.CloseTicket;
using EnterpriseTicketing.Application.Tickets.Commands.CreateTicket;
using EnterpriseTicketing.Application.Tickets.Commands.EscalateTicket;
using EnterpriseTicketing.Application.Tickets.Commands.UpdateTicket;
using EnterpriseTicketing.Application.Tickets.Queries.GetTicketById;
using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using EnterpriseTicketing.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseTicketing.API.Controllers.v1;

/// <summary>
/// Tickets API v1
///
/// Controller design principles:
///   1. Thin controllers — no business logic here. Controllers map HTTP → commands/queries.
///   2. All logic lives in application handlers, not controllers.
///   3. Controllers return ActionResult<T> for type-safe response mapping.
///   4. ProducesResponseType attributes enable accurate Swagger documentation.
///   5. CancellationToken propagated to all async calls for graceful request cancellation.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets")]
[Authorize]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ISender sender, ILogger<TicketsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Get a paginated list of tickets with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<TicketSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedResponse<TicketSummaryDto>>> GetTickets(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TicketStatus? status = null,
        [FromQuery] TicketPriority? priority = null,
        [FromQuery] TicketCategory? category = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] bool sortDesc = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTicketsQuery
        {
            PageNumber = Math.Max(1, pageNumber),
            PageSize = Math.Clamp(pageSize, 1, 100),
            Status = status,
            Priority = priority,
            Category = category,
            CustomerId = customerId,
            AssignedToUserId = assignedTo,
            SearchTerm = search,
            SortBy = sortBy,
            SortDescending = sortDesc
        };

        var result = await _sender.Send(query, cancellationToken);

        return Ok(new PaginatedResponse<TicketSummaryDto>
        {
            Items = result.Items,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage
        });
    }

    /// <summary>
    /// Get a ticket by its ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TicketDetailDto>> GetTicket(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetTicketByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new support ticket.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> CreateTicket(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateTicketCommand
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Category = request.Category,
            CustomerId = request.CustomerId,
            AssignedToUserId = request.AssignedToUserId
        };

        var ticketId = await _sender.Send(command, cancellationToken);

        _logger.LogInformation("Ticket {TicketId} created via API", ticketId);

        return CreatedAtAction(nameof(GetTicket), new { id = ticketId }, new { id = ticketId });
    }

    /// <summary>
    /// Update ticket details (title, description, priority, category).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTicket(
        [FromRoute] Guid id,
        [FromBody] UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        await _sender.Send(new UpdateTicketCommand
        {
            TicketId = id,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Category = request.Category
        }, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Close a ticket. Terminal state — cannot be reopened.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseTicket(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        await _sender.Send(new CloseTicketCommand { TicketId = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Escalate a ticket. Increments escalation level and may auto-upgrade priority.
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateTicket(
        [FromRoute] Guid id,
        [FromBody] EscalateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        await _sender.Send(new EscalateTicketCommand
        {
            TicketId = id,
            Reason = request.Reason
        }, cancellationToken);

        return NoContent();
    }
}

public sealed record EscalateTicketRequest
{
    public required string Reason { get; init; }
}
