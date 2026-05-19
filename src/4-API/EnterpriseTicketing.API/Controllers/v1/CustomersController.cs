using Asp.Versioning;
using EnterpriseTicketing.Application.Tickets.Queries.GetTickets;
using EnterpriseTicketing.Application.Tickets.Queries.GetTicketsByCustomer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseTicketing.API.Controllers.v1;

/// <summary>
/// Customers API v1. Currently exposes a read-only view that lets support agents
/// pivot from a customer to their full ticket history - the most common workflow
/// when a customer calls in.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
[Authorize]
[Produces("application/json")]
public sealed class CustomersController(IMediator mediator, ILogger<CustomersController> logger) : ControllerBase
{
    /// <summary>Returns all tickets belonging to a customer.</summary>
    [HttpGet("{customerId:guid}/tickets")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TicketSummaryDto>>> GetTicketsForCustomer(
        [FromRoute] Guid customerId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Fetching tickets for customer {CustomerId}", customerId);
        var result = await mediator.Send(
            new GetTicketsByCustomerQuery { CustomerId = customerId },
            cancellationToken);
        return Ok(result);
    }
}
