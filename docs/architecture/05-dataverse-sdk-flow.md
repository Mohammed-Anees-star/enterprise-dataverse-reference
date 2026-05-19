# 05 - Dataverse SDK Flow

`Microsoft.PowerPlatform.Dataverse.Client.ServiceClient` is a thread-safe, connection-pooled
client. Registered as a Singleton.

```
Handler -> ITicketRepository -> IDataverseService -> ServiceClient
                                                       │
                                                       ▼  (Polly wrap policy)
                                                  CircuitBreaker -> Retry
                                                       │
                                                       ▼
                                            Dataverse OrgService (SOAP)
```

Use the SDK when:
- Running server-side with trusted credentials
- Issuing complex FetchXML queries
- Needing access to the full request/response shape (e.g., `ExecuteMultipleRequest`)
- Server-side plugin compatibility

See ADR-001 in `19-tradeoff-analysis.md` for the SDK vs Web API decision.
