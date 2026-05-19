# 12 - Middleware Pipeline

Order matters. Anything below the exception handler is wrapped by it; anything above
runs before the exception handler can format the response.

```
1. UseSerilogRequestLogging      // capture start time, status code
2. CorrelationIdMiddleware       // stamp X-Correlation-ID
3. SecurityHeadersMiddleware     // patches response headers
4. ExceptionHandlingMiddleware   // RFC 7807 ProblemDetails
5. UseHttpsRedirection
6. UseCors
7. UseAuthentication             // identify the caller
8. UseAuthorization              // authorize the caller
9. MapControllers                // MVC pipeline (ApiExceptionFilter etc.)
```

Why this order:

- Correlation ID must be set BEFORE the exception handler so error responses carry it.
- Security headers must be added BEFORE the response is committed.
- Authentication must come AFTER CORS - CORS pre-flight is unauthenticated.
- ExceptionHandlingMiddleware sits high so it sees exceptions from auth pipeline too.
