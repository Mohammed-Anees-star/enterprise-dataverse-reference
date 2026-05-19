using System.Diagnostics;
using System.Text.Json;
using EnterpriseTicketing.Application.Common.Exceptions;
using EnterpriseTicketing.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using ApplicationException = EnterpriseTicketing.Application.Common.Exceptions.ApplicationException;
using ValidationException = EnterpriseTicketing.Application.Common.Exceptions.ValidationException;

namespace EnterpriseTicketing.API.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
///
/// RFC 7807 (Problem Details for HTTP APIs) provides a machine-readable format for errors.
/// This enables API clients to parse error details programmatically rather than scraping
/// human-readable messages. Most enterprise API gateways (Azure APIM, etc.) understand this format.
///
/// Security note: Never expose stack traces, internal paths, or sensitive data in API responses.
/// Log these details server-side (with correlation ID for tracing), return minimal safe info to client.
///
/// Exception → HTTP Status mapping:
///   ValidationException      → 422 Unprocessable Entity (field-level validation errors)
///   NotFoundException        → 404 Not Found
///   ForbiddenAccessException → 403 Forbidden
///   DomainException          → 400 Bad Request (business rule violation)
///   All others               → 500 Internal Server Error (unexpected)
/// </summary>
public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, problem) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status422UnprocessableEntity,
                CreateValidationProblem(validationEx, context, correlationId)),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                CreateProblem("Not Found", notFoundEx.Message, StatusCodes.Status404NotFound, context, correlationId)),

            ForbiddenAccessException forbiddenEx => (
                StatusCodes.Status403Forbidden,
                CreateProblem("Forbidden", forbiddenEx.Message, StatusCodes.Status403Forbidden, context, correlationId)),

            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                CreateDomainProblem(domainEx, context, correlationId)),

            ApplicationException appEx => (
                StatusCodes.Status400BadRequest,
                CreateProblem("Application Error", appEx.Message, StatusCodes.Status400BadRequest, context, correlationId)),

            _ => (
                StatusCodes.Status500InternalServerError,
                CreateProblem("Internal Server Error",
                    "An unexpected error occurred. Please contact support with the correlation ID.",
                    StatusCodes.Status500InternalServerError, context, correlationId))
        };

        // Log with appropriate severity
        if (statusCode >= 500)
            _logger.LogError(exception,
                "Unhandled exception {ExceptionType}. CorrelationId: {CorrelationId}",
                exception.GetType().Name, correlationId);
        else if (statusCode >= 400)
            _logger.LogWarning(exception,
                "Handled {ExceptionType} → {StatusCode}. CorrelationId: {CorrelationId}",
                exception.GetType().Name, statusCode, correlationId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
    }

    private static ProblemDetails CreateProblem(
        string title, string detail, int statusCode, HttpContext context, string correlationId)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Instance = context.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };
    }

    private static ValidationProblemDetails CreateValidationProblem(
        ValidationException ex, HttpContext context, string correlationId)
    {
        return new ValidationProblemDetails(ex.Errors)
        {
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Instance = context.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };
    }

    private static ProblemDetails CreateDomainProblem(
        DomainException ex, HttpContext context, string correlationId)
    {
        return new ProblemDetails
        {
            Title = "Business Rule Violation",
            Detail = ex.Message,
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["errorCode"] = ex.ErrorCode
            }
        };
    }
}
