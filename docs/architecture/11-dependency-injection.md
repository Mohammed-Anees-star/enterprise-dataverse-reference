# 11 - Dependency Injection

Each layer exposes one extension method (`AddApplication`, `AddInfrastructure`)
so the API/Functions hosts compose the graph with three lines.

Lifetime conventions:

| Type | Lifetime |
|---|---|
| Repositories, Services | Scoped (one per request) |
| ServiceClient, ServiceBusClient | Singleton (thread-safe, expensive) |
| HttpClient via IHttpClientFactory | Lifetime managed by factory |
| MediatR handlers | Scoped (auto-registered by MediatR) |
| MediatR pipeline behaviors | Scoped |
| FluentValidation validators | Scoped (auto-registered) |
| BackgroundService | Singleton (by ASP.NET convention) |
| IMemoryCache | Singleton |
