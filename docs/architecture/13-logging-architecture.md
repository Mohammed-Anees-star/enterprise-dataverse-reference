# 13 - Logging Architecture

Serilog as the structured logger, sinks to Console (dev) and Application Insights (prod).

```
ILogger<T>  -> Serilog provider  -> Sinks
                  │
                  ├─ Enrichers: FromLogContext, MachineName, ThreadId, CorrelationId
                  ├─ Filters:   per-namespace minimum levels
                  ▼
                  Application Insights (TraceTelemetry) + Console
```

Conventions:
- Structured properties only. Never string-format into the message template.
- Use `BeginScope` for cross-message context (CorrelationId, MessageId, UserId).
- LogInformation for business events. LogDebug for fine-grained flow. LogWarning
  for retries / soft errors. LogError for human-actionable failures.

PII: `LogContext.PushProperty("EmailAddress", value)` is forbidden; emails are
redacted at the Serilog enricher stage via a destructure-by-policy rule.
