using Azure.Messaging.ServiceBus;
using EnterpriseTicketing.Application.Common.Interfaces;
using EnterpriseTicketing.Domain.Entities;
using EnterpriseTicketing.Domain.Enums;
using EnterpriseTicketing.Domain.Interfaces;
using EnterpriseTicketing.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace EnterpriseTicketing.API.IntegrationTests.Infrastructure;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TProgram}"/> that:
///   1. Switches the environment to "Testing" so Swagger is rendered and dev-only
///      middleware is active while production-only paths are skipped.
///   2. Removes real Dataverse, Service Bus, and background services.
///   3. Registers in-memory test doubles so the full middleware + MediatR +
///      validation pipeline runs without any real external dependencies.
///   4. Installs a no-op authentication scheme so [Authorize] attributes can be
///      satisfied by setting a custom header in test requests.
///
/// Usage pattern (see TicketsControllerTests):
///   factory.CreateAuthenticatedClient(roles: ["Ticket.Writer"])
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Shared mutable in-memory store; tests can seed data before each request.
    private readonly InMemoryTicketStore _ticketStore = new();
    private readonly InMemoryCustomerStore _customerStore = new();

    public InMemoryTicketStore Tickets => _ticketStore;
    public InMemoryCustomerStore Customers => _customerStore;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // ── Remove real infrastructure registrations ──────────────────────
            services.RemoveAll<ITicketRepository>();
            services.RemoveAll<ICustomerRepository>();
            services.RemoveAll<IDataverseService>();
            services.RemoveAll<IDataverseTokenProvider>();
            services.RemoveAll<IDataverseWebApiService>();
            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<ITicketNumberSequence>();
            services.RemoveAll<IEventBus>();
            services.RemoveAll<ICurrentUserService>();
            services.RemoveAll<ServiceBusClient>();

            // Remove background services that would try to connect to real infrastructure
            services.RemoveAll<IHostedService>();

            // ── Register test doubles ─────────────────────────────────────────
            services.AddSingleton<ITicketRepository>(_ticketStore);
            services.AddSingleton<ICustomerRepository>(_customerStore);

            // Sequence: deterministic incrementing counter
            services.AddSingleton<ITicketNumberSequence>(new InMemorySequence());

            // Outbox: captured in-memory for assertion
            var outbox = new InMemoryOutboxStore();
            services.AddSingleton<IOutboxStore>(outbox);
            services.AddSingleton(outbox);  // expose for assertions

            // Event bus: no-op (outbox relay is disabled in tests)
            services.AddScoped<IEventBus>(_ => Mock.Of<IEventBus>());

            // CurrentUserService: default "test-user" with all roles
            services.AddScoped<ICurrentUserService>(_ =>
                new TestCurrentUserService("test-user-id", "Test User"));

            // ── Authentication: replace JWT Bearer with test scheme ────────────
            services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });
        });
    }

    /// <summary>Creates an <see cref="HttpClient"/> that sends requests as an authenticated user.</summary>
    public HttpClient CreateAuthenticatedClient(
        string userId = "test-user-id",
        string[]? roles = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add(
            "X-Test-Roles",
            string.Join(",", roles ?? ["Ticket.Reader", "Ticket.Writer", "Ticket.Manager", "Ticket.Admin"]));
        return client;
    }
}
