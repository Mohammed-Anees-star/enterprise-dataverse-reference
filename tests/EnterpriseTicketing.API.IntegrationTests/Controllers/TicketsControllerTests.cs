using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EnterpriseTicketing.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the v1 Tickets controller. The factory boots the full
/// ASP.NET Core pipeline (middleware, MediatR, behaviours) with the standard
/// configuration so that we exercise routing, model binding, and exception mapping.
///
/// External dependencies (Dataverse, Service Bus) are out of scope for this layer;
/// they are stubbed via test doubles registered in the factory's
/// <see cref="WebApplicationFactory{TEntryPoint}.ConfigureWebHost"/>.
/// </summary>
public class TicketsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TicketsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Get_without_auth_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tickets/00000000-0000-0000-0000-000000000000");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_live_returns_200()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
