# 14 - Exception Handling

Three layers of defence:

1. Pipeline behavior `UnhandledExceptionBehavior` - logs every exception that escapes a handler.
2. MVC filter `ApiExceptionFilter` - maps known exception types to ProblemDetails.
3. Middleware `ExceptionHandlingMiddleware` - last-resort handler for anything outside MVC.

Mapping table:

| Exception | HTTP | Body |
|---|---|---|
| ValidationException | 422 | RFC 7807 with `errors` dictionary |
| NotFoundException | 404 | RFC 7807 |
| ForbiddenAccessException | 403 | RFC 7807 |
| DomainException | 400 | RFC 7807 + `errorCode` extension |
| Anything else | 500 | RFC 7807 (no stack trace in prod) |

Stack traces are NEVER returned in production. Correlation ID is always included so the
caller can quote it to support.
