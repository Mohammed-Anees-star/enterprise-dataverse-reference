using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EnterpriseTicketing.API.Filters;

/// <summary>
/// MVC action filter that converts known exception types into RFC 7807 ProblemDetails responses.
///
/// We register both the ExceptionHandlingMiddleware (catches everything below MVC) and this
/// action filter (catches exceptions inside MVC) so that filters and controllers can throw
/// freely and still get a proper response.
/// </summary>
public sealed class ApiExceptionFilter : IActionFilter, IOrderedFilter
{
    public int Order => int.MaxValue - 10;

    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Exception is null) return;

        switch (context.Exception)
        {
            case ValidationException ve:
                context.Result = new ObjectResult(new ValidationProblemDetails(ve.Errors)
                {
                    Title = "Validation failed",
                    Status = StatusCodes.Status422UnprocessableEntity
                })
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
                context.ExceptionHandled = true;
                break;

            case NotFoundException nfe:
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Resource not found",
                    Detail = nfe.Message,
                    Status = StatusCodes.Status404NotFound
                })
                { StatusCode = StatusCodes.Status404NotFound };
                context.ExceptionHandled = true;
                break;

            case ForbiddenAccessException fae:
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = fae.Message,
                    Status = StatusCodes.Status403Forbidden
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;

            case DomainException de:
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Domain rule violation",
                    Detail = de.Message,
                    Status = StatusCodes.Status400BadRequest,
                    Extensions = { ["errorCode"] = de.ErrorCode }
                })
                { StatusCode = StatusCodes.Status400BadRequest };
                context.ExceptionHandled = true;
                break;
        }
    }
}
