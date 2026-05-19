using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseTicketing.API.Controllers.v2;

/// <summary>
/// Tickets API v2 — demonstrates API versioning evolution.
///
/// Enterprise API versioning strategy:
///   - v1 remains backward compatible for existing clients
///   - v2 adds new capabilities without breaking v1
///   - Deprecation cycle: announce deprecation, provide migration guide, sunset after 12+ months
///   - Use URL path versioning for maximum visibility (not query string or headers)
///
/// This controller shows the pattern — in production it would contain
/// the enhanced v2 operations (e.g., bulk operations, richer filtering,
/// expanded response models with linked resources).
/// </summary>
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/tickets")]
[Authorize]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    private readonly ISender _sender;

    public TicketsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// [v2] Enhanced ticket retrieval with embedded customer details.
    /// Demonstrates how API evolution works with versioning.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketV2(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        // v2 would use a richer query that includes embedded customer/comment data
        // For this reference implementation, delegates to the same v1 handler
        var result = await _sender.Send(
            new Application.Tickets.Queries.GetTicketById.GetTicketByIdQuery(id),
            cancellationToken);

        // v2 response could include hypermedia links (HATEOAS), embedded resources, etc.
        return Ok(new
        {
            result.Id,
            result.TicketNumber,
            result.Title,
            result.Status,
            result.Priority,
            result.Category,
            result.CreatedAt,
            _links = new
            {
                self = Url.Action(nameof(GetTicketV2), new { id }),
                close = Url.Action("CloseTicket", "Tickets", new { id }),
                escalate = Url.Action("EscalateTicket", "Tickets", new { id })
            }
        });
    }
}
